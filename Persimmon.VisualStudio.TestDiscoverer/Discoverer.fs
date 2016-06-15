namespace Persimmon.VisualStudio.TestDiscoverer

open System
open System.Diagnostics
open System.IO
open System.Linq
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Range
open Microsoft.FSharp.Compiler.SourceCodeServices

open Persimmon.VisualStudio.TestRunner.Internals

//////////////////////////////////////////////////////////
// Private AST visitor implementation

module private DiscovererImpl = 

  /// Traverse AST with this context information.
  [<Sealed>]
  type DiscoverContext private (symbolNames, range) = 
    
    /// Constructor.
    new() = DiscoverContext([||], range())
    
    /// Nest context.
    member __.Nest(name, range) = 
      DiscoverContext([ name ]
                      |> (Seq.append symbolNames)
                      |> Seq.toArray, range)

    /// Unnest context.
    member __.Decrease(range) =
      DiscoverContext(symbolNames
        |> Seq.take(symbolNames.Length - 1)
        |> Seq.toArray, range)
    
    /// Construct SymbolInformation.
    member __.ToSymbolInformation() = 
      new SymbolInformation(symbolNames |> String.concat ".", range.FileName, range.StartLine, range.EndLine, 
                            range.StartColumn, range.EndColumn)
  
  /// Nest context by name.
  let nestFromName (context : DiscoverContext) (name : string) range = context.Nest(name, range)
  
  /// Nest context by ident.
  let nestFromIdent (context : DiscoverContext) (ident : Ident) range = context.Nest(ident.idText, range)
  
  /// Nest context by long ident.
  let nestFromLongIdent (context : DiscoverContext) (lid : LongIdent) range = 
    let names = 
      String.concat "." [ for i in lid -> i.idText ]
    context.Nest(names, range)
  
  /// Try get detailed test name (test title):
  ///  expr:
  ///    App (Expr0):
  ///      Ident: test   <-- Ident required (value is ignore)
  ///    App (Expr1):
  ///      Const:
  ///        String: "success test(list)"  <-- Return test title string.
  /// TODO: Ugly match expr...
  let tryGetTestName = 
    function 
    | SynExpr.App(_, _, expr0, expr1, _) -> 
      match expr0 with
      | SynExpr.Ident(_) -> // Ident
        match expr1 with
        | SynExpr.Const(c, _) -> // Const
          match c with
          | SynConst.String(str, range) -> // String
            Some(str, range)
          | _ -> None
        | _ -> None
      | _ -> None
    | _ -> None
  
  /// Expression visit.
  let rec visitExpressionInternal (context : DiscoverContext) expr : SymbolInformation seq = 
    seq { 
      match expr with
      | SynExpr.App(_, _, expr0, expr1, _) -> 
        yield! visitExpression context expr0
        yield! visitExpression context expr1
      | SynExpr.CompExpr(_, _, expr, _) -> yield! visitExpression context expr
      // tests, tests2, tests32
      | SynExpr.ArrayOrListOfSeqExpr(_, expr, _) -> yield! visitExpression context expr
      // tests, tests2, tests32
      | SynExpr.Sequential(_, _, expr0, expr1, _) -> 
        yield! visitExpression context expr0
        yield! visitExpression context expr1
      // tests3, tests32
      | SynExpr.YieldOrReturn(_, expr, _) -> yield! visitExpression context expr
      // tests5
      | SynExpr.Paren(expr, _, _, _) -> yield! visitExpression context expr
      // tests5
      | SynExpr.Lambda(_, _, _, expr, _) -> yield! visitExpression context expr
      // tests6
      | SynExpr.LetOrUse(_, _, bindings, body, _) -> 
        for binding in bindings do
          yield! visitBinding context binding
        yield! visitExpression context body
      | _ -> ()
    }
  
  /// Expression pre-interpret test titling and visit.
  and visitExpression (context : DiscoverContext) expr : SymbolInformation seq = 
    seq { 
      let nest =
        match expr with
        | SynExpr.App(_, _, expr0, _, _) ->
          match tryGetTestName expr0 with
          | Some(name, range) -> Some(nestFromName context name range)
          | None ->
            match tryGetTestName expr with
            | Some(name, range) ->
              // DIRTY HACK: If expr is short than expr0, must decrease context.
              //   tests6 : "Persimmon.Sample.tests6.innerTest.source parameterize test" --> "Persimmon.Sample.tests6.source parameterize test"
              let decreasedContext = context.Decrease range
              Some(nestFromName decreasedContext name range)
            | None -> None
        | _ -> None
      match nest with
      | Some namedContext -> 
        yield namedContext.ToSymbolInformation()
        yield! visitExpressionInternal namedContext expr
      | None -> yield! visitExpressionInternal context expr
    }
  
  /// Expression pre-interpret symbol naming (on let binding) and visit.
  and visitBinding (context : DiscoverContext) binding : SymbolInformation seq = 
    seq { 
      let (Binding(_, _, _, _, _, _, _, pat, _, body, _, _)) = binding
      match pat with
      | SynPat.Named(_, name, _, _, range) -> 
        let namedContext = nestFromIdent context name range
        yield namedContext.ToSymbolInformation()
        yield! visitExpressionInternal namedContext body
      | SynPat.LongIdent(LongIdentWithDots(lid, _), _, _, _, _, range) -> 
        let nest = nestFromLongIdent context lid range
        yield nest.ToSymbolInformation()
        yield! visitExpressionInternal nest body
      | _ -> yield! visitExpressionInternal context body
    }
  
  /// Expression let binding and visit.
  let visitBindings (context : DiscoverContext) bindings : SymbolInformation seq = 
    seq { 
      for binding in bindings do
        yield! visitBinding context binding
    }
  
  /// Type definition and visit.
  let visitTypeDefinition (context : DiscoverContext) typeDefine : SymbolInformation seq = 
    seq { 
      match typeDefine with
      | SynTypeDefn.TypeDefn(cinfo, SynTypeDefnRepr.ObjectModel(_, members, _), _, _) -> 
        let (SynComponentInfo.ComponentInfo(_, _, _, lid, _, _, _, range)) = cinfo
        let nest = nestFromLongIdent context lid range
        for memberDefine in members do
          match memberDefine with
          | SynMemberDefn.LetBindings(bindings, _, _, _) -> yield! visitBindings nest bindings
          | SynMemberDefn.Member(binding, _) -> yield! visitBinding nest binding
          | _ -> ()
      | _ -> ()
    }
  
  /// Module level declaration and visit.
  let rec visitDeclarations (context : DiscoverContext) decls : SymbolInformation seq = 
    seq { 
      for declaration in decls do
        match declaration with
        | SynModuleDecl.NestedModule(cinfo, nestedDecls, _, _) -> 
          let (SynComponentInfo.ComponentInfo(_, _, _, lid, _, _, _, range)) = cinfo
          let nest = nestFromLongIdent context lid range
          yield! visitDeclarations nest nestedDecls
        // Module's let binding
        | SynModuleDecl.Let(_, bindings, _) -> yield! visitBindings context bindings
        // Type definition
        | SynModuleDecl.Types(typeDefines, _) -> 
          for typeDefine in typeDefines do
            yield! visitTypeDefinition context typeDefine
        | _ -> ()
    }
  
  /// File level declaration and visit.
  let visitModulesAndNamespaces (context : DiscoverContext) modulesOrNss : SymbolInformation seq = 
    seq { 
      for moduleOrNs in modulesOrNss do
        let (SynModuleOrNamespace(lid, _, decls, _, _, _, range)) = moduleOrNs
        let nest = nestFromLongIdent context lid range
        yield! visitDeclarations nest decls
    }
  
  /// Traverse AST.
  let visitTreeRoot (results : FSharpParseFileResults) : SymbolInformation seq = 
    seq { 
      match results.ParseTree.Value with
      | ParsedInput.ImplFile(implFile) -> 
        let (ParsedImplFileInput(_, _, _, _, _, modules, _)) = implFile
        let context = DiscoverContext()
        yield! visitModulesAndNamespaces context modules
      | _ -> ()
    }

//////////////////////////////////////////////////////////
// Public (MBR) interface

[<Sealed>]
type Discoverer() = 
  inherit MarshalByRefObject()
  
  let rec traverseFsprojRecursive basePath fsprojName =
    match basePath with
    | null -> None
    | _ ->
      let fsprojPath = Path.Combine(basePath, fsprojName)
      match File.Exists fsprojPath with
      | true -> Some fsprojPath
      | false -> 
        let parentPath = Path.GetDirectoryName basePath
        traverseFsprojRecursive parentPath fsprojName
  
  let traverseFsproj assemblyPath = 
    let basePath = Path.GetDirectoryName assemblyPath
    let fsprojName = (Path.GetFileNameWithoutExtension assemblyPath) + ".fsproj"
    traverseFsprojRecursive basePath fsprojName
  
  let asyncParseCode projOptions path : Async<SymbolInformation []> = 
    async { 
      try
        let checker = FSharpChecker.Create()
        let sourceCodeText = File.ReadAllText(path)
        let! results = checker.ParseFileInProject(path, sourceCodeText, projOptions)
        return DiscovererImpl.visitTreeRoot results |> Seq.toArray
      with
      | _ as ex ->
        Trace.WriteLine(ex.ToString())
        return [||]
    }
  
  let asyncParseCodes projOptions : Async<SymbolInformation []> = 
    async { 
      try
        // TODO: ProjectCracker cannot retrieve source code file paths,
        //   try improvement ProjectCracker and send PR? :)
        let! results = projOptions.OtherOptions
                       |> Seq.filter (fun opt -> opt.StartsWith("-") = false)
                       |> Seq.map (fun path -> asyncParseCode projOptions path)
                       |> Async.Parallel
        return (results |> Seq.collect (fun result -> result)).Distinct()
               |> Seq.toArray
      with
      | _ as ex ->
        Trace.WriteLine(ex.ToString())
        return [||]
    }
  
  /// Discover and gather symbol informations.
  member __.AsyncDiscover targetAssemblyPath : Async<SymbolInformation []> = 
    match traverseFsproj targetAssemblyPath with
    | Some fsprojPath ->
      try
        let projOptions = ProjectCracker.GetProjectOptionsFromProjectFile fsprojPath
        asyncParseCodes projOptions
      with
      | _ as ex ->
        Trace.WriteLine(ex.ToString())
        async { return [||] }
    | None -> async { return [||] }
  
  interface IDiscoverer with
    /// Discover and gather symbol informations synchronously.
    member this.Discover targetAssemblyPath =
      this.AsyncDiscover targetAssemblyPath |> Async.RunSynchronously
