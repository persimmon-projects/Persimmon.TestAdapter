namespace Persimmon.VisualStudio.TestDiscoverer

open System
open System.IO
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.SourceCodeServices

open Persimmon.VisualStudio.TestRunner.Internals

module private DiscovererImpl =

    let rec visitPattern (indent: string) pat = seq {
        match pat with
        | SynPat.Wild(_) -> 
            printfn "%sUnderscore" indent
        | SynPat.Named(pat, name, _, _, _) ->
            yield! visitPattern (indent + "  ") pat
            printfn "%sNamed: '%s'" indent name.idText
        | SynPat.LongIdent(LongIdentWithDots(ident, _), _, _, _, _, _) ->
            let names = String.concat "." [ for i in ident -> i.idText ]
            printfn "%sLongIdent: %s" indent names
//        | pat -> printfn "%sその他のパターン: %A" pat
        | _ -> ()
    }

    let visitSimplePatterns (indent: string) pats = seq {
        match pats with
        | SynSimplePats.SimplePats(simplepats, _) ->
            for pat in simplepats do
                match pat with
                | SynSimplePat.Id(ident, _, _, _, _, _) ->
                    printfn "%sSimplePat.Id: '%s'" indent ident.idText
                | _ -> ()
        | _ -> ()
    }

    let visitConst (indent: string) value = seq {
        match value with
        | SynConst.String(str, _) ->
            printfn "%sString: \"%s\"" indent str
        | _ -> ()
    }

    let rec visitExpression (indent: string) expr = seq {
        match expr with
        // tests6
        | SynExpr.LetOrUse(_, _, bindings, body, _) ->
            printfn "%sLetOrUse (Expr):" indent
            for binding in bindings do
            let (Binding(access, kind, inlin, mutabl, attrs, xmlDoc, 
                            data, pat, retInfo, init, m, sp)) = binding
            yield! visitPattern (indent + "  ") pat
            yield! visitExpression (indent + "  ") init
            printfn "%sLetOrUse (Body):" indent
            yield! visitExpression (indent + "  ") body
        | SynExpr.App(_, _, expr0, expr1, _) ->
            printfn "%sApp (Expr0):" indent
            yield! visitExpression (indent + "  ") expr0
            printfn "%sApp (Expr1):" indent
            yield! visitExpression (indent + "  ") expr1
        // test
        | SynExpr.Ident id ->
            printfn "%sIdent: %A" indent id
        // 'hogehoge'
        | SynExpr.Const(c, _) ->
            printfn "%sConst:" indent
            yield! visitConst (indent + "  ") c
        // tests, tests2, tests32
        | SynExpr.ArrayOrListOfSeqExpr(_, expr, _) ->
            printfn "%sArrayOrListOfSeqExpr:" indent
            yield! visitExpression (indent + "  ") expr
        | SynExpr.CompExpr(_, _, expr, _) ->
            printfn "%sCompExpr:" indent
            yield! visitExpression (indent + "  ") expr
        | SynExpr.Sequential(info, _, expr0, expr1, _) ->
            printfn "%sSequential: %A" indent info
            let indent1 = indent + "  "
            printfn "%s[0]:" indent1
            yield! visitExpression (indent1 + "  ") expr0
            printfn "%s[1]:" indent1
            yield! visitExpression (indent1 + "  ") expr1
        // tests3, tests32
        | SynExpr.YieldOrReturn(_, expr, _) ->
            printfn "%sYieldOrReturn:" indent
            yield! visitExpression (indent + "  ") expr
        // tests5
        | SynExpr.Paren(expr, _, _, _) ->
            printfn "%sParen:" indent
            yield! visitExpression (indent + "  ") expr
        // tests5
        | SynExpr.Lambda(_, _, pats, expr, _) ->
            printfn "%sLambda:" indent
            yield! visitSimplePatterns (indent + "  ") pats
            yield! visitExpression (indent + "  ") expr
//        | expr -> printfn "%sサポート対象外の式: %A" indent expr
        | _ -> ()
    }

    let visitBinding (indent: string) binding = seq {
        let (Binding(access, kind, inlin, mutabl, attrs, xmlDoc, 
                    data, pat, retInfo, body, m, sp)) = binding
        yield! visitPattern indent pat
        yield! visitExpression indent body
    }

    let visitBindings (indent: string) bindings = seq {
        for binding in bindings do
            yield! visitBinding indent binding
    }

    let visitTypeDefine (indent: string) typeDefine = seq {
        match typeDefine with
        | SynTypeDefn.TypeDefn(
                              SynComponentInfo.ComponentInfo(_, args, _, ident, _, _, _, _),
                              SynTypeDefnRepr.ObjectModel(kind, members, _),
                              _, _) ->
            let names = String.concat "." [ for i in ident -> i.idText ]
            printfn "%sTypeDefn: %s" indent names
            for memberDefine in members do
                match memberDefine with
                | SynMemberDefn.LetBindings(bindings, _, _, _) ->
                    printfn "%sType Let:" indent
                    yield! visitBindings (indent + "  ") bindings
                | SynMemberDefn.Member(binding, _) ->
                    printfn "%sType Member:" indent
                    yield! visitBinding (indent + "  ") binding
                | _ -> ()
        | _ -> ()
    }

    let visitDeclarations (indent: string) decls = seq {
        for declaration in decls do
            match declaration with
            // Basic module's binding
            | SynModuleDecl.Let(isRec, bindings, range) ->
                printfn "%sModule Let:" indent
                yield! visitBindings (indent + "  ") bindings
            // MyClass
            | SynModuleDecl.Types(typeDefines, _) ->
                printfn "%sModule Types:" indent
                for typeDefine in typeDefines do
                    yield! visitTypeDefine (indent + "  ") typeDefine
//            | _ -> printfn "%sサポート対象外の宣言: %A" indent declaration
            | _ -> ()
    }

    let visitModulesAndNamespaces (indent: string) modulesOrNss = seq {
        for moduleOrNs in modulesOrNss do
            let (SynModuleOrNamespace(lid, isMod, decls, xml, attrs, _, m)) = moduleOrNs
            printfn "%sModuleOrNamespace: %A" indent lid
            yield! visitDeclarations "  " decls
    }

    let visitResults (results: FSharpParseFileResults) = seq {
        match results.ParseTree.Value with
        | ParsedInput.ImplFile(implFile) ->
            // 宣言を展開してそれぞれを走査する
            let (ParsedImplFileInput(fn, script, name, _, _, modules, _)) = implFile
            yield! visitModulesAndNamespaces "" modules
        | _ -> failwith "F# インターフェイスファイル (*.fsi) は未サポートです。"
    }

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

  let asyncParseCode projOptions path = async {
    let checker = FSharpChecker.Create()
    let sourceCodeText = File.ReadAllText(path)
    let! results = checker.ParseFileInProject(path, sourceCodeText, projOptions)
    return DiscovererImpl.visitResults results |> Seq.toArray
  }

  let asyncParseCodes projOptions = async {
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
