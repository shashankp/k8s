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

MSBuildLocator.RegisterDefaults();
using var workspace = MSBuildWorkspace.Create();

if (args.Length == 0)
{
    Console.WriteLine("Usage: CsParser <path-to-solution>");
    return;
}

var solutionPath = args[0];
var solution = await workspace.OpenSolutionAsync(solutionPath);
foreach (var project in solution.Projects)
{
    Console.WriteLine($"Project: {project.Name}");
    var compilation = await project.GetCompilationAsync();
    foreach (var document in project.Documents)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null) continue;
        var root = await syntaxTree.GetRootAsync();
        var classes = root?.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classes ?? Enumerable.Empty<ClassDeclarationSyntax>())
        {
            var namespaceDecl = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var namespaceName = namespaceDecl != null ? namespaceDecl.Name.ToString() : "Global";
            await graphDbService.CreateRecordNodeAsync(classDecl.Identifier.ValueText, namespaceName, classDecl.ToString());
        }

        var records = root?.DescendantNodes().OfType<RecordDeclarationSyntax>();
        foreach (var recordDecl in records ?? Enumerable.Empty<RecordDeclarationSyntax>())
        {
            var namespaceDecl = recordDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var namespaceName = namespaceDecl != null ? namespaceDecl.Name.ToString() : "Global";
            await graphDbService.CreateRecordNodeAsync(recordDecl.Identifier.ValueText, namespaceName, recordDecl.ToString());

            var properties = recordDecl.ParameterList?.Parameters;
            foreach(var prop in properties ?? Enumerable.Empty<ParameterSyntax>())
            {
                if (prop.Type == null) continue;
                await graphDbService.CreateDependency(recordDecl.Identifier.ValueText, prop.Type.ToString());
            }
        }
    }
}