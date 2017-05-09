// Copyright 2011-2015

//     Tomas Petricek
// 	Ben Winkel
//     Igor Siguta 
// 	Robin Neatherway
//     Dave Thomas

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

(*

see also https://github.com/ionide/FsAutoComplete/pull/55

Change log:

  * rename namespace
  * rename module
  * from Choice to Result
*)

namespace Persimmon.TestDiscoverer

open System
open System.IO
open Microsoft.FSharp.Compiler.SourceCodeServices

module ProjectCracker =
  let GetProjectOptionsFromResponseFile (file : string)  =
    let projDir = Path.GetDirectoryName file
    let rsp =
      Directory.GetFiles(projDir, "dotnet-compile-fsc.rsp", SearchOption.AllDirectories)
      |> Seq.head
      |> File.ReadAllLines
      |> Array.map (fun s -> if s.EndsWith ".fs" then
                                let p = Path.GetFullPath s
                                (p.Chars 0).ToString().ToLower() + p.Substring(1)
                             else s )
      |> Array.filter((<>) "--nocopyfsharpcore")

    {
      ProjectFileName = file
      ProjectFileNames = [||]
      OtherOptions = rsp
      ReferencedProjects = [||]
      IsIncompleteTypeCheckEnvironment = false
      UseScriptResolutionRules = false
      LoadTime = DateTime.Now
      UnresolvedReferences = None;
      OriginalLoadReferences = []
      ExtraProjectInfo = None
    }

  let runProcess (workingDir: string) (exePath: string) (args: string) =
      let psi = System.Diagnostics.ProcessStartInfo()
      psi.FileName <- exePath
      psi.WorkingDirectory <- workingDir 
      psi.RedirectStandardOutput <- false
      psi.RedirectStandardError <- false
      psi.Arguments <- args
      psi.CreateNoWindow <- true
      psi.UseShellExecute <- false

      use p = new System.Diagnostics.Process()
      p.StartInfo <- psi
      p.Start() |> ignore
      p.WaitForExit()
      
      let exitCode = p.ExitCode
      exitCode, ()

  let GetProjectOptionsFromProjectFile (file : string) =
    let rec projInfo file =
        let projDir = Path.GetDirectoryName file

        let runCmd exePath args = runProcess projDir exePath (args |> String.concat " ")

        let msbuildExec = Dotnet.ProjInfo.Inspect.dotnetMsbuild runCmd
        let getFscArgs = Dotnet.ProjInfo.Inspect.getFscArgs
        let getP2PRefs = Dotnet.ProjInfo.Inspect.getP2PRefs
        let gp () = Dotnet.ProjInfo.Inspect.getProperties ["TargetPath"]
        let log = ignore

        let results =
          file
          |> Dotnet.ProjInfo.Inspect.getProjectInfos log msbuildExec [getFscArgs; getP2PRefs; gp] []
    
        // $(TargetPath)
        let mutable rsp : string list = []
        let mutable p2p : string list = []
        let mutable props : (string * string) list = []

        let doResult result =
          match result with
          | Ok (Dotnet.ProjInfo.Inspect.GetResult.FscArgs x) -> rsp <- x
          | Ok (Dotnet.ProjInfo.Inspect.GetResult.P2PRefs x) -> p2p <- x
          | Ok (Dotnet.ProjInfo.Inspect.GetResult.Properties p) -> props <- p
          | Error _ -> failwith "errors"

        match results with
        | Ok r -> r |> List.iter doResult
        | Error r -> failwith "errors"

        //TODO cache projects info of p2p ref
        let p2pProjects = p2p |> List.map projInfo

        let tar =
            match props |> Map.ofList |> Map.tryFind "TargetPath" with
            | Some t -> t
            | None -> failwith "error, 'TargetPath' property not found"

        let po =
            {
                ProjectFileName = file
                ProjectFileNames = [||]
                OtherOptions = rsp |> Array.ofList
                ReferencedProjects = p2pProjects |> Array.ofList
                IsIncompleteTypeCheckEnvironment = false
                UseScriptResolutionRules = false
                LoadTime = DateTime.Now
                UnresolvedReferences = None;
                OriginalLoadReferences = []
                ExtraProjectInfo = None
            }

        tar, po

    let _, po = projInfo file
    po