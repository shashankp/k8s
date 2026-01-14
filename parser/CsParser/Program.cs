using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;

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
}