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
await graphDbService.Backup();
await graphDbService.ClearDatabase();

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
    if (compilation == null) continue;

    foreach (var document in project.Documents)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null || syntaxTree == null) continue;

        var root = await syntaxTree.GetRootAsync();
        if (root == null) continue;

        await Processor.ProcessClasses(root, semanticModel, document, graphDbService);
        await Processor.ProcessRecords(root, semanticModel, document, graphDbService);
        await Processor.ProcessMethods(root, semanticModel, graphDbService);
        await Processor.ProcessMethodCalls(root, semanticModel, graphDbService);
        await Processor.ProcessDiagnostics(compilation, syntaxTree, root, semanticModel, graphDbService);
    }
}

await graphDbService.CreateVectorIndex();
