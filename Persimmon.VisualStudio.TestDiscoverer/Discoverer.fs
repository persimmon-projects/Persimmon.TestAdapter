namespace Persimmon.VisualStudio.TestDiscoverer

open System
open System.IO
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Range
open Microsoft.FSharp.Compiler.SourceCodeServices

open Persimmon.VisualStudio.TestRunner.Internals

//////////////////////////////////////////////////////////
// Private AST visitor implementation

module private DiscovererImpl =

    /// Traverse AST with this context information.
    [<Sealed>]
    type DiscoverContext private (symbolNames: string[], range: range) =
        new() = DiscoverContext([||], range())
        member __.Indent =
            symbolNames |> Seq.map (fun _ -> String.Empty) |> String.concat "  "
        member __.Nest(name: string, range) =
            DiscoverContext([name] |> (Seq.append symbolNames) |> Seq.toArray, range)
        member __.ToSymbolInformation() =
            SymbolInformation(
                symbolNames |> String.concat ".",
                range.FileName,
                range.StartLine,
                range.EndLine,
                range.StartColumn,
                range.EndColumn)

    // Try get detailed test name (test title):
    //  expr:
    //    App (Expr0):
    //        App (Expr00):
    //          Ident: test   <-- Ident required (value is ignore)
    //        App (Expr01):
    //          Const:
    //            String: "success test(list)"  <-- Return test title string.
    //    App (Expr1):
    //        ArrayOrListOfSeqExpr:  <-- Return test title with "contextSeq=true" (ex: = context "Hoge" [ ... ])
    //        CompExpr:              <-- or Other nodes "contextSeq=false"
    let tryGetTestName = function
        | SynExpr.App(_, _, expr0, expr1, _) ->
            match expr0 with
            | SynExpr.App(_, _, expr00, expr01, _) ->
                match expr00 with
                | SynExpr.Ident(_) ->   // Ident
                    match expr01 with
                    | SynExpr.Const(c, _) ->    // Const
                        match c with
                        | SynConst.String(str, range) ->    // String
                            let contextSeq =
                                match expr1 with
                                | SynExpr.ArrayOrListOfSeqExpr(_, _, _) -> true
                                | _ -> false
                            Some (str, contextSeq, range)
                        | _ -> None
                    | _ -> None
                | _ -> None
            | _ -> None
        | _ -> None

    /// Expression visit.
    let rec visitExpressionInternal (context: DiscoverContext) expr : SymbolInformation seq = seq {
        match expr with
        | SynExpr.App(_, _, expr0, expr1, _) ->
            yield! visitExpression context expr0
            yield! visitExpression context expr1
        | SynExpr.CompExpr(_, _, expr, _) ->
            yield! visitExpression context expr
        // tests, tests2, tests32
        | SynExpr.ArrayOrListOfSeqExpr(_, expr, _) ->
            yield! visitExpression context expr
        // tests, tests2, tests32
        | SynExpr.Sequential(_, _, expr0, expr1, range) ->
            yield! visitExpression context expr0
            yield! visitExpression context expr1
        // tests3, tests32
        | SynExpr.YieldOrReturn(_, expr, _) ->
            //printfn "%sYieldOrReturn:" context.Indent
            yield! visitExpression context expr
        // tests5
        | SynExpr.Paren(expr, _, _, _) ->
            //printfn "%sParen:" context.Indent
            yield! visitExpression context expr
        // tests5
        | SynExpr.Lambda(_, _, _, expr, _) ->
            yield! visitExpression context expr
        // tests6
        | SynExpr.LetOrUse(_, _, bindings, body, _) ->
            for binding in bindings do
                yield! visitBinding context binding
            yield! visitExpression context body
        | _ -> ()
      }

    /// Expression pre-interpret test titling and visit.
    and visitExpression (context: DiscoverContext) expr : SymbolInformation seq = seq {
        let nest =
            match tryGetTestName expr with
            | Some (name, _, range) -> Some (context.Nest(name, range))
            | None -> None
        match nest with
        | Some namedContext ->
            yield namedContext.ToSymbolInformation()
            yield! visitExpressionInternal namedContext expr
        | None ->
            yield! visitExpressionInternal context expr
      }

    /// Expression pre-interpret symbol naming (on let binding) and visit.
    and visitBinding (context: DiscoverContext) binding : SymbolInformation seq = seq {
        let (Binding(access, kind, inlin, mutabl, attrs, xmlDoc, 
                    data, pat, retInfo, body, m, sp)) = binding
        match pat with
        | SynPat.Named(_, name, _, _, range) ->
            let namedContext =
                match tryGetTestName body with
                | Some (cname, contextSeq, crange) ->
                    match contextSeq with
                    | true -> context.Nest(name.idText + "." + cname, crange)
                    | false -> context.Nest(name.idText, range)
                | None -> context.Nest(name.idText, range)
            yield namedContext.ToSymbolInformation()
            yield! visitExpressionInternal namedContext body
        | SynPat.LongIdent(LongIdentWithDots(ident, _), _, _, _, _, range) ->
            let names = String.concat "." [ for i in ident -> i.idText ]
            let namedContext = context.Nest(names, range)
            yield namedContext.ToSymbolInformation()
            yield! visitExpressionInternal namedContext body
        | _ ->
            yield! visitExpressionInternal context body
      }

    /// Expression let binding and visit.
    let visitBindings (context: DiscoverContext) bindings : SymbolInformation seq = seq {
        for binding in bindings do
            yield! visitBinding context binding
    }

    /// Type definition and visit.
    let visitTypeDefinition (context: DiscoverContext) typeDefine : SymbolInformation seq = seq {
        match typeDefine with
        | SynTypeDefn.TypeDefn(
                              SynComponentInfo.ComponentInfo(_, args, _, ident, _, _, _, range),
                              SynTypeDefnRepr.ObjectModel(kind, members, _),
                              _, _) ->
            let names = String.concat "." [ for i in ident -> i.idText ]
            let namedContext = context.Nest(names, range)
            for memberDefine in members do
                match memberDefine with
                | SynMemberDefn.LetBindings(bindings, _, _, _) ->
                    yield! visitBindings namedContext bindings
                | SynMemberDefn.Member(binding, _) ->
                    yield! visitBinding namedContext binding
                | _ -> ()
        | _ -> ()
    }

    /// Module level declaration and visit.
    let visitDeclarations (context: DiscoverContext) decls : SymbolInformation seq = seq {
        for declaration in decls do
            match declaration with
            // Module's let binding
            | SynModuleDecl.Let(isRec, bindings, range) ->
                yield! visitBindings context bindings
            // Type definition
            | SynModuleDecl.Types(typeDefines, _) ->
                for typeDefine in typeDefines do
                    yield! visitTypeDefinition context typeDefine
            | _ -> ()
    }

    /// File level declaration and visit.
    let visitModulesAndNamespaces (context: DiscoverContext) modulesOrNss : SymbolInformation seq = seq {
        for moduleOrNs in modulesOrNss do
            let (SynModuleOrNamespace(lid, isMod, decls, xml, attrs, _, range)) = moduleOrNs
            let names = String.concat "." [ for i in lid -> i.idText ]
            let nest = context.Nest(names, range)
            yield! visitDeclarations nest decls
    }

    /// Traverse AST.
    let visitTreeRoot (results: FSharpParseFileResults) : SymbolInformation seq = seq {
        match results.ParseTree.Value with
        | ParsedInput.ImplFile(implFile) ->
            let (ParsedImplFileInput(fn, script, name, _, _, modules, _)) = implFile
            let context = DiscoverContext()
            yield! visitModulesAndNamespaces context modules
        | _ -> ()
    }

//////////////////////////////////////////////////////////
// Public (MBR) interface

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

    let asyncParseCode projOptions path : Async<SymbolInformation[]> = async {
        let checker = FSharpChecker.Create()
        let sourceCodeText = File.ReadAllText(path)
        let! results = checker.ParseFileInProject(path, sourceCodeText, projOptions)
        return DiscovererImpl.visitTreeRoot results |> Seq.toArray
    }

    let asyncParseCodes projOptions : Async<SymbolInformation[]> = async {
        let! results =
            projOptions.OtherOptions |>
            Seq.filter (fun opt -> opt.StartsWith("--") = false) |>
            Seq.map (fun path -> asyncParseCode projOptions path) |>
            Async.Parallel
        return results |> Seq.collect (fun result -> result) |> Seq.toArray
    }

    member __.AsyncDiscover targetAssemblyPath : Async<SymbolInformation[]> =
        let fsprojPath = traverseFsproj targetAssemblyPath
        let projOptions = ProjectCracker.GetProjectOptionsFromProjectFile fsprojPath
        asyncParseCodes projOptions

    interface IDiscoverer with
        member this.Discover targetAssemblyPath =
            this.AsyncDiscover targetAssemblyPath |> Async.RunSynchronously
