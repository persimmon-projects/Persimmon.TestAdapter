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

    [<Sealed>]
    type DiscoverContext private (symbolNames: string[], range: range) =
        new() = DiscoverContext([||], range())
        member __.Indent =
            symbolNames |> Seq.map (fun _ -> String.Empty) |> String.concat "  "
        member __.Nest(name: string, range) =
            DiscoverContext([name] |> (Seq.append symbolNames) |> Seq.toArray, range)
        member __.ToSymbolInformation() =
            SymbolInformation(symbolNames |> String.concat ".", range.FileName, range.StartLine, range.EndLine)

    let rec visitPattern (context: DiscoverContext) pat : SymbolInformation seq = seq {
        match pat with
//        | SynPat.Wild(range) -> 
//            let nest = context.Nest("_", range)
//            yield nest.ToSymbolInformation()
//            //printfn "%sUnderscore" context.Indent
        | SynPat.Named(pat, name, _, _, _) ->
            let nest = context.Nest(name.idText, name.idRange)
            yield! visitPattern nest pat
            yield nest.ToSymbolInformation()
            //printfn "%sNamed: '%s'" context.Indent name.idText
        | SynPat.LongIdent(LongIdentWithDots(ident, _), _, _, _, _, range) ->
            let names = String.concat "." [ for i in ident -> i.idText ]
            let nest = context.Nest(names, range)
            yield nest.ToSymbolInformation()
            //printfn "%sLongIdent: %s" context.Indent names
//        | pat -> printfn "%sその他のパターン: %A" context.Indent pat
        | _ -> ()
    }

    let visitSimplePatterns (context: DiscoverContext) pats : SymbolInformation seq = seq {
        match pats with
        | SynSimplePats.SimplePats(simplepats, _) ->
            for pat in simplepats do
                match pat with
                | SynSimplePat.Id(ident, _, _, _, _, range) ->
                    let nest = context.Nest(ident.idText, range)
                    yield nest.ToSymbolInformation()
                    //printfn "%sSimplePat.Id: '%s'" context.Indent ident.idText
                | _ -> ()
        | _ -> ()
    }

    let visitConst (context: DiscoverContext) value : SymbolInformation seq = seq {
        match value with
        | SynConst.String(str, range) ->
            let nest = context.Nest(str, range)
            yield nest.ToSymbolInformation()
            //printfn "%sString: \"%s\"" context.Indent str
        | _ -> ()
    }

    let rec visitExpression (context: DiscoverContext) expr : SymbolInformation seq = seq {
        match expr with
        // tests6
        | SynExpr.LetOrUse(_, _, bindings, body, _) ->
            //printfn "%sLetOrUse (Expr):" context.Indent
            for binding in bindings do
                let (Binding(access, kind, inlin, mutabl, attrs, xmlDoc, 
                                data, pat, retInfo, init, m, sp)) = binding
                yield! visitPattern context pat
                yield! visitExpression context init
            //printfn "%sLetOrUse (Body):" context.Indent
            yield! visitExpression context body
        | SynExpr.App(_, _, expr0, expr1, _) ->
            //printfn "%sApp (Expr0):" context.Indent
            yield! visitExpression context expr0
            //printfn "%sApp (Expr1):" context.Indent
            yield! visitExpression context expr1
        // test
        | SynExpr.Ident id ->
            //printfn "%sIdent: %A" context.Indent id
            let nest = context.Nest(id.idText, id.idRange)
            yield nest.ToSymbolInformation()
        // 'hogehoge'
        | SynExpr.Const(c, _) ->
            //printfn "%sConst:" context.Indent
            yield! visitConst context c
        // tests, tests2, tests32
        | SynExpr.ArrayOrListOfSeqExpr(_, expr, _) ->
            //printfn "%sArrayOrListOfSeqExpr:" context.Indent
            yield! visitExpression context expr
        | SynExpr.CompExpr(_, _, expr, _) ->
            //printfn "%sCompExpr:" context.Indent
            yield! visitExpression context expr
        | SynExpr.Sequential(info, _, expr0, expr1, range) ->
            //printfn "%sSequential: %A" context.Indent info
            let nest0 = context.Nest("[0]", expr0.Range)
            //printfn "%s[0]:" indent1
            yield! visitExpression nest0 expr0
            let nest1 = context.Nest("[1]", expr1.Range)
            //printfn "%s[1]:" indent1
            yield! visitExpression nest1 expr1
        // tests3, tests32
        | SynExpr.YieldOrReturn(_, expr, _) ->
            //printfn "%sYieldOrReturn:" context.Indent
            yield! visitExpression context expr
        // tests5
        | SynExpr.Paren(expr, _, _, _) ->
            //printfn "%sParen:" context.Indent
            yield! visitExpression context expr
        // tests5
        | SynExpr.Lambda(_, _, pats, expr, _) ->
            //printfn "%sLambda:" context.Indent
            yield! visitSimplePatterns context pats
            yield! visitExpression context expr
//        | expr -> printfn "%sサポート対象外の式: %A" context.Indent expr
        | _ -> ()
    }

    let visitBinding (context: DiscoverContext) binding : SymbolInformation seq = seq {
        let (Binding(access, kind, inlin, mutabl, attrs, xmlDoc, 
                    data, pat, retInfo, body, m, sp)) = binding
        yield! visitPattern context pat
        yield! visitExpression context body
    }

    let visitBindings (context: DiscoverContext) bindings : SymbolInformation seq = seq {
        for binding in bindings do
            yield! visitBinding context binding
    }

    let visitTypeDefine (context: DiscoverContext) typeDefine : SymbolInformation seq = seq {
        match typeDefine with
        | SynTypeDefn.TypeDefn(
                              SynComponentInfo.ComponentInfo(_, args, _, ident, _, _, _, _),
                              SynTypeDefnRepr.ObjectModel(kind, members, _),
                              _, _) ->
            let names = String.concat "." [ for i in ident -> i.idText ]
            //printfn "%sTypeDefn: %s" context.Indent names
            for memberDefine in members do
                match memberDefine with
                | SynMemberDefn.LetBindings(bindings, _, _, _) ->
                    //printfn "%sType Let:" context.Indent
                    yield! visitBindings context bindings
                | SynMemberDefn.Member(binding, _) ->
                    //printfn "%sType Member:" context.Indent
                    yield! visitBinding context binding
                | _ -> ()
        | _ -> ()
    }

    let visitDeclarations (context: DiscoverContext) decls : SymbolInformation seq = seq {
        for declaration in decls do
            match declaration with
            // Basic module's binding
            | SynModuleDecl.Let(isRec, bindings, range) ->
                //printfn "%sModule Let:" context.Indent
                yield! visitBindings context bindings
            // MyClass
            | SynModuleDecl.Types(typeDefines, _) ->
                //printfn "%sModule Types:" context.Indent
                for typeDefine in typeDefines do
                    yield! visitTypeDefine context typeDefine
//            | _ -> printfn "%sサポート対象外の宣言: %A" context.Indent declaration
            | _ -> ()
    }

    let visitModulesAndNamespaces (context: DiscoverContext) modulesOrNss : SymbolInformation seq = seq {
        for moduleOrNs in modulesOrNss do
            let (SynModuleOrNamespace(lid, isMod, decls, xml, attrs, _, range)) = moduleOrNs
            //printfn "%sModuleOrNamespace: %A" context.Indent lid
            let names = String.concat "." [ for i in lid -> i.idText ]
            let nest = context.Nest(names, range)
            yield! visitDeclarations nest decls
    }

    let visitResults (results: FSharpParseFileResults) : SymbolInformation seq = seq {
        match results.ParseTree.Value with
        | ParsedInput.ImplFile(implFile) ->
            // 宣言を展開してそれぞれを走査する
            let (ParsedImplFileInput(fn, script, name, _, _, modules, _)) = implFile
            let context = DiscoverContext()
            yield! visitModulesAndNamespaces context modules
        | _ -> failwith "F# インターフェイスファイル (*.fsi) は未サポートです。"
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
    return DiscovererImpl.visitResults results |> Seq.toArray
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
