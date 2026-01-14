using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Neo4j.Driver;
using DotNetEnv;
using CsParser;

DotNetEnv.Env.Load();
var neo4jUrl = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
var neo4jUser = Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
var neo4jPassword = Environment.GetEnvironmentVariable("NEO4J_PASSWORD");
var driver = GraphDatabase.Driver(neo4jUrl, AuthTokens.Basic(neo4jUser, neo4jPassword));
var graphDbService = new GraphDbService(driver);
graphDbService.Backup().Wait();
graphDbService.ClearDatabase().Wait();

MSBuildLocator.RegisterDefaults();
using var workspace = MSBuildWorkspace.Create();

if (args.Length == 0)
{
    Console.WriteLine("Usage: CsParser <path-to-solution>");
    return;
}

var solutionPath = args[0];
var solution = await workspace.OpenSolutionAsync(solutionPath);
const int ClassDependencyThreshold = 10;
foreach (var project in solution.Projects)
{
    Console.WriteLine($"Project: {project.Name}");
    var compilation = await project.GetCompilationAsync();
    if (compilation == null) continue;

    foreach (var document in project.Documents)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null || syntaxTree == null) continue;

        var root = await syntaxTree.GetRootAsync();
        if (root == null) continue;        

        var classes = root?.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classes ?? Enumerable.Empty<ClassDeclarationSyntax>())
        {
            var dependencies = new HashSet<string>();
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            var fullName = classSymbol?.ToDisplayString() ?? classDecl.Identifier.Text;
            var namespaceName = classSymbol?.ContainingNamespace.ToDisplayString() ?? "Global";
            await graphDbService.CreateClassNodeAsync(classDecl.Identifier.ValueText, namespaceName, classDecl.ToString());

            if (classSymbol?.BaseType != null && classSymbol.BaseType.SpecialType != SpecialType.System_Object)
            {
                await graphDbService.CreateInheritance(fullName, classSymbol.BaseType.ToDisplayString());
            }

            var classProperties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
            foreach (var prop in classProperties)
            {
                var propInfo = semanticModel.GetSymbolInfo(prop.Type);
                var typeSymbol = propInfo.Symbol ?? propInfo.CandidateSymbols.FirstOrDefault();
                if (typeSymbol == null) continue;
                dependencies.Add(typeSymbol.ToDisplayString());
                await graphDbService.CreateDependency(fullName, typeSymbol.ToDisplayString());
            }

            if (dependencies.Count > ClassDependencyThreshold)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Diagnostic] God Class Detected: {fullName} handles {dependencies.Count} unique types.");
                Console.ResetColor();
                await graphDbService.TagGodClassAsync(fullName);
            }
        }

        var records = root?.DescendantNodes().OfType<RecordDeclarationSyntax>();
        foreach (var recordDecl in records ?? Enumerable.Empty<RecordDeclarationSyntax>())
        {
            var recordSymbol = semanticModel.GetDeclaredSymbol(recordDecl);
            var fullName = recordSymbol?.ToDisplayString() ?? recordDecl.Identifier.Text;
            var namespaceName = recordSymbol?.ContainingNamespace.ToDisplayString() ?? "Global";

            await graphDbService.CreateRecordNodeAsync(recordDecl.Identifier.ValueText, namespaceName, recordDecl.ToString());

            var properties = recordDecl.ParameterList?.Parameters;
            foreach (var prop in properties ?? Enumerable.Empty<ParameterSyntax>())
            {
                if (prop.Type == null) continue;
                var propInfo = semanticModel.GetSymbolInfo(prop.Type);
                var typeSymbol = propInfo.Symbol ?? propInfo.CandidateSymbols.FirstOrDefault();
                if (typeSymbol == null) continue;
                await graphDbService.CreateDependency(fullName, typeSymbol.ToDisplayString());
            }
        }

        var methodCalls = root?.DescendantNodes().OfType<InvocationExpressionSyntax>();
        if (methodCalls == null) continue;
        foreach (var call in methodCalls)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(call);
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
            if (methodSymbol == null) continue;
            var callerClass = call.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (callerClass != null)
            {
                var callerSymbol = semanticModel.GetDeclaredSymbol(callerClass);
                if (callerSymbol == null) continue;
                await graphDbService.CreateMethodCall(callerSymbol.ToDisplayString(), methodSymbol.ToDisplayString());
            }
        }

        var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Location.SourceTree == syntaxTree &&
                (d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error));
        foreach (var diagnostic in diagnostics)
        {
            var category = diagnostic.Id.StartsWith("SCS") ? "Security"
                : diagnostic.Id.StartsWith("CA18") || diagnostic.Id.StartsWith("CA19") ? "Performance"
                : "Compiler";

            var location = diagnostic.Location;
            if (location.IsInSource)
            {
                var node = root.FindNode(location.SourceSpan);
                if (node == null) continue;
                var classDecl = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

                if (classDecl != null)
                {
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
                    var fullName = classSymbol?.ToDisplayString();
                    if (fullName != null)
                    {
                        await graphDbService.CreateDiagnosticIssue(
                            fullName,
                            diagnostic.Id,
                            diagnostic.GetMessage(),
                            diagnostic.Severity.ToString(),
                            category);
                    }
                }
            }
        }
    }
}