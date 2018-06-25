#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System

let outDir = "bin"

let configuration = getBuildParamOrDefault "configuration" "Release"

let project = "Persimmon.TestAdapter"

// File system information
let solutionFile  = "Persimmon.TestAdapter.sln"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "persimmon-projects"

// The name of the project on GitHub
let gitName = "Persimmon.TestAdapter"

// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|Shproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | f when f.EndsWith("shproj") -> Shproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product project
          Attribute.Version release.AssemblyVersion
          Attribute.FileVersion release.AssemblyVersion
          Attribute.Configuration configuration ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> CreateFSharpAssemblyInfo (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> CreateCSharpAssemblyInfo ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> CreateVisualBasicAssemblyInfo ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> ()
        )
)

// Copies binaries from default VS location to exepcted bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs
Target "CopyBinaries" (fun _ ->
  !! "src/**/*.??proj"
  |>  Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) @@ "bin" @@ configuration, outDir @@ (System.IO.Path.GetFileNameWithoutExtension f)))
  |>  Seq.iter (fun (fromDir, toDir) -> CopyDir toDir fromDir (fun _ -> true))
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
  CleanDirs [outDir; "temp"]
  !! ("./src/**/bin" @@ configuration)
  |> CleanDirs
)

Target "CleanDocs" (fun _ ->
  CleanDirs ["docs/output"]
)

Target "Build" (fun _ ->

  DotNetCli.Restore (fun p ->
    { p with
        Project = solutionFile
    }
  )

  !! solutionFile
  |> MSBuild "" "Rebuild" [ ("Platform", "Any CPU"); ("Configuration", configuration) ]
  |> ignore
)

Target "RunTests" (fun _ ->
  !! "tests/**/*.Tests.fsproj"
  |> Seq.iter (fun f ->
    DotNetCli.Test (fun p -> { p with Project = f })
  )
)

Target "NuGet" (fun _ ->

  Paket.Pack(fun p ->
    { p with
        OutputPath = outDir
        Version = release.NugetVersion
        ReleaseNotes = toLines release.Notes})
)

Target "PublishNuget" (fun _ ->
  Paket.Push(fun p -> { p with WorkingDir = outDir })
)

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "Release" (fun _ ->
  StageAll ""
  Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
  Branches.pushBranch "" "origin" "master"

  Branches.tag "" release.NugetVersion
  Branches.pushTag "" "origin" release.NugetVersion

  // release on github
  createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
  |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
  // TODO: |> uploadFile "PATH_TO_FILE"
  |> releaseDraft
  |> Async.RunSynchronously
)

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "CopyBinaries"
  ==> "RunTests"
  ==> "All"

"All"
  ==> "NuGet"

"NuGet"
  ==> "PublishNuget"
  ==> "Release"

RunTargetOrDefault "All"
