using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Neo4j.Driver;
namespace CsParser;

public class Processor
{
    public static async Task ProcessClasses(SyntaxNode root, SemanticModel semanticModel, Document document, GraphDbService service)
    {
        const int ClassDependencyThreshold = 100;
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classes)
        {
            var dependencies = new HashSet<string>();
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            var fullName = classSymbol?.ToDisplayString() ?? classDecl.Identifier.Text;
            var namespaceName = classSymbol?.ContainingNamespace.ToDisplayString() ?? "Global";

            await service.CreateClassNodeAsync(classDecl.Identifier.ValueText,
                namespaceName, classDecl.ToString(), document.FilePath ?? "Unknown");

            if (classSymbol?.BaseType != null && classSymbol.BaseType.SpecialType != SpecialType.System_Object)
            {
                await service.CreateInheritance(fullName, classSymbol.BaseType.ToDisplayString());
            }

            foreach (var interfaceType in classSymbol?.Interfaces ?? Enumerable.Empty<INamedTypeSymbol>())
            {
                await service.CreateInterfaceImplementation(fullName, interfaceType.ToDisplayString());
            }

            var classProperties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
            foreach (var prop in classProperties)
            {
                var propInfo = semanticModel.GetSymbolInfo(prop.Type);
                var typeSymbol = propInfo.Symbol ?? propInfo.CandidateSymbols.FirstOrDefault();
                if (typeSymbol == null) continue;
                if (dependencies.Add(typeSymbol.ToDisplayString()))
                {
                    await service.CreateDependency(fullName, typeSymbol.ToDisplayString());
                }
                if (typeSymbol is ITypeSymbol type)
                {
                    await ExtractGenericTypeArguments(type, dependencies, fullName, service);
                }
            }

            var classFields = classDecl.Members.OfType<FieldDeclarationSyntax>();
            foreach (var field in classFields)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldInfo = semanticModel.GetSymbolInfo(field.Declaration.Type);
                    var typeSymbol = fieldInfo.Symbol ?? fieldInfo.CandidateSymbols.FirstOrDefault();
                    if (typeSymbol == null) continue;

                    if (dependencies.Add(typeSymbol.ToDisplayString()))
                    {
                        await service.CreateDependency(fullName, typeSymbol.ToDisplayString());
                    }
                    if (typeSymbol is ITypeSymbol type)
                    {
                        await ExtractGenericTypeArguments(type, dependencies, fullName, service);
                    }
                }
            }

            var attributes = classDecl.AttributeLists.SelectMany(al => al.Attributes);
            foreach (var attr in attributes)
            {
                var attrSymbol = semanticModel.GetSymbolInfo(attr).Symbol;
                if (attrSymbol != null)
                {
                    var attrName = attrSymbol.ContainingType.ToDisplayString();
                    await service.CreateAttributeUsage(fullName, attrName);
                }
            }

            if (dependencies.Count > ClassDependencyThreshold)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Diagnostic] God Class Detected: {fullName} handles {dependencies.Count} unique types.");
                Console.ResetColor();
                await service.TagGodClassAsync(fullName);
            }
        }
    }

    public static async Task ProcessRecords(SyntaxNode root, SemanticModel semanticModel, Document document, GraphDbService service)
    {
        var records = root.DescendantNodes().OfType<RecordDeclarationSyntax>();

        foreach (var recordDecl in records)
        {
            var recordSymbol = semanticModel.GetDeclaredSymbol(recordDecl);
            var fullName = recordSymbol?.ToDisplayString() ?? recordDecl.Identifier.Text;
            var namespaceName = recordSymbol?.ContainingNamespace.ToDisplayString() ?? "Global";

            await service.CreateRecordNodeAsync(recordDecl.Identifier.ValueText,
                namespaceName, recordDecl.ToString(), document.FilePath ?? "Unknown");

            var properties = recordDecl.ParameterList?.Parameters;
            foreach (var prop in properties ?? Enumerable.Empty<ParameterSyntax>())
            {
                if (prop.Type == null) continue;
                var propInfo = semanticModel.GetSymbolInfo(prop.Type);
                var typeSymbol = propInfo.Symbol ?? propInfo.CandidateSymbols.FirstOrDefault();
                if (typeSymbol == null) continue;
                await service.CreateDependency(fullName, typeSymbol.ToDisplayString());
            }
        }
    }

    public static async Task ProcessMethods(SyntaxNode root, SemanticModel semanticModel, GraphDbService service)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var methodDecl in methods)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl);
            if (methodSymbol == null) continue;

            var containingClass = methodDecl.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingClass == null) continue;

            var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);
            if (classSymbol == null) continue;

            var methodFullName = methodSymbol.ToDisplayString();

            if (methodSymbol.ReturnType.SpecialType != SpecialType.System_Void)
            {
                await service.CreateMethodReturnType(methodFullName, methodSymbol.ReturnType.ToDisplayString());
            }

            foreach (var param in methodSymbol.Parameters)
            {
                await service.CreateMethodParameter(methodFullName, param.Type.ToDisplayString());
            }

            // Track caught exceptions
            var catchClauses = methodDecl.DescendantNodes().OfType<CatchClauseSyntax>();
            foreach (var catchClause in catchClauses)
            {
                if (catchClause.Declaration?.Type != null)
                {
                    var exceptionTypeInfo = semanticModel.GetSymbolInfo(catchClause.Declaration.Type);
                    var exceptionType = exceptionTypeInfo.Symbol as ITypeSymbol;
                    if (exceptionType != null)
                    {
                        await service.CreateExceptionHandler(methodFullName, exceptionType.ToDisplayString());
                    }
                }
            }

            if (methodDecl.Body != null)
            {
                var dataFlow = semanticModel.AnalyzeDataFlow(methodDecl.Body);
                if (dataFlow == null) continue;
                foreach (var symbol in dataFlow.DataFlowsIn)
                {
                    if (symbol is ILocalSymbol localSymbol)
                    {
                        await service.CreateVariableRead(methodFullName, localSymbol.Name, localSymbol.Type.ToDisplayString());
                    }
                }

                var ifStatements = methodDecl.Body.DescendantNodes().OfType<IfStatementSyntax>();
                foreach (var ifStmt in ifStatements)
                {
                    var condition = ifStmt.Condition.ToString();
                    await service.CreateBranchCondition(methodFullName, condition);
                }

                var switchStatementsCount = methodDecl.Body.DescendantNodes().OfType<SwitchStatementSyntax>().Count();
                var branchCount = ifStatements.Count() + switchStatementsCount;
                if (branchCount > 0)
                {
                    await service.AddBranchingComplexity(methodFullName, branchCount);
                }

            }
        }
    }

    public static async Task ProcessMethodCalls(SyntaxNode root, SemanticModel semanticModel, GraphDbService service)
    {
        var methodCalls = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

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
                await service.CreateMethodCall(callerSymbol.ToDisplayString(), methodSymbol.ToDisplayString());
            }
        }
    }

    public static async Task ProcessDiagnostics(Compilation compilation, SyntaxTree syntaxTree, SyntaxNode root,
        SemanticModel semanticModel, GraphDbService service)
    {
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
                        await service.CreateDiagnosticIssue(fullName, diagnostic.Id,
                            diagnostic.GetMessage(), diagnostic.Severity.ToString(), category);
                    }
                }
            }
        }
    }

    static async Task ExtractGenericTypeArguments(ITypeSymbol typeSymbol, HashSet<string> dependencies,
        string fullName, GraphDbService service)
    {
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var typeArg in namedType.TypeArguments)
            {
                if (dependencies.Add(typeSymbol.ToDisplayString()))
                {
                    await service.CreateDependency(fullName, typeSymbol.ToDisplayString());
                }
                if (typeArg is ITypeSymbol type)
                {
                    await ExtractGenericTypeArguments(type, dependencies, fullName, service);
                }
            }
        }
    }
}