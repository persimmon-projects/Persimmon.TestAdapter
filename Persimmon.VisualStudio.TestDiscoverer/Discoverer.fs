namespace Persimmon.VisualStudio.TestDiscoverer

open System
open System.IO
open Microsoft.FSharp.Compiler.SourceCodeServices
open Persimmon.VisualStudio.TestRunner.Internals

[<Sealed>]
type Discoverer () =
  inherit MarshalByRefObject ()

  let rec traverseFsprojRecursive basePath fsprojName =
    let fsprojPath = Path.Combine(basePath, fsprojName)
    match File.Exists fsprojPath with
    | true -> fsprojPath
    | false ->
      let parentPath = Path.GetDirectoryName basePath
      traverseFsprojRecursive parentPath fsprojName

  let traverseFsproj assemblyPath =
    let basePath = Path.GetDirectoryName assemblyPath
    let fsprojName = (Path.GetFileNameWithoutExtension assemblyPath) + ".fsproj"
    traverseFsprojRecursive basePath fsprojName

  member __.AsyncDiscover targetAssemblyPath = async {
    let fsprojPath = traverseFsproj targetAssemblyPath
    let checker = FSharpChecker.Create()
    let projOptions = ProjectCracker.GetProjectOptionsFromProjectFile fsprojPath
    let! results = checker.ParseAndCheckProject projOptions
    let! allUses = results.GetAllUsesOfAllSymbols()
    return allUses |> Seq.map (fun symbol -> symbol.) |> Seq.toArray
  }

  interface IDiscoverer with
    member this.Discover targetAssemblyPath =
      this.AsyncDiscover targetAssemblyPath |> Async.RunSynchronously
