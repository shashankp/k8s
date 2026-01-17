using Neo4j.Driver;
using SmartComponents.LocalEmbeddings;

namespace CsParser;

public class GraphDbService
{
    private readonly IDriver _driver;
    private readonly LocalEmbedder _embedder;

    public GraphDbService(IDriver driver)
    {
        _driver = driver;
        _embedder = new LocalEmbedder();
    }

    public async Task Backup()
    {
        var filename = Path.Combine(Path.GetTempPath(), $"neo4j_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync("MATCH (n)-[r]->(m) RETURN n, r, m");
        var records = await result.ToListAsync();
        var json = System.Text.Json.JsonSerializer.Serialize(records);
        await File.WriteAllTextAsync(filename, json);

        Console.WriteLine($"Backup saved to: {filename}");
    }


    public async Task ClearDatabase()
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync("MATCH (n) DETACH DELETE n");
    }

    public async Task CreateRecordNodeAsync(string name, string nameSpace, string code, string filePath)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "CREATE (n:Record {sname: $name, shortName: $shortName, namespace: $nameSpace, code: $code, embedding: $embedding, filePath: $filePath})",
            new { name = name, shortName = Utils.GetShortName(name), nameSpace, code, embedding = _embedder.Embed(code).Values.ToArray(), filePath }
        );
        Console.WriteLine($"Record: {name}");
    }

    public async Task CreateClassNodeAsync(string name, string nameSpace, string code, string filePath)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "CREATE (n:Class {name: $name, shortName: $shortName, namespace: $nameSpace, code: $code, embedding: $embedding, filePath: $filePath})",
            new { name = name, shortName = Utils.GetShortName(name), nameSpace, code, embedding = _embedder.Embed(code).Values.ToArray(), filePath }
        );
        Console.WriteLine($"Class: {name}");
    }

    public async Task CreateDependency(string recordName, string typeName)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (t:Type {name: $typeName}) " +
            "WITH t " +
            "MATCH (r:Record {name: $recordName}) " +
            "MERGE (r)-[:DEPENDS_ON]->(t)",
            new { recordName, typeName }
        );
        Console.WriteLine($"    Type dependency from {recordName} to {typeName}");
    }

    public async Task CreateAttributeUsage(string className, string attributeName)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (a:Attribute {name: $attributeName}) " +
            "MERGE (c:Class {name: $className}) " +
            "MERGE (c)-[:HAS_ATTRIBUTE]->(a)",
            new { className, attributeName });
    }

    public async Task CreateInheritance(string className, string baseTypeName)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (c:Class {name: $className}) " +
            "MERGE (b:Type {name: $baseTypeName}) " +
            "MERGE (c)-[:INHERITS]->(b)",
            new { className, baseTypeName });
    }
    public async Task CreateVariableRead(string methodName, string variableName, string variableType)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (m:Method {name: $methodName}) " +
            "MERGE (v:Variable {name: $variableName, type: $variableType}) " +
            "MERGE (m)-[:READS_VARIABLE]->(v)",
            new { methodName, variableName, variableType });
    }

    public async Task CreateMethodCall(string fromClass, string methodName)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (m:Method {name: $methodName}) " +
            "WITH m " +
            "MATCH (c:Class {name: $fromClass}) " +
            "MERGE (c)-[:CALLS]->(m)",
            new { fromClass, methodName });
    }

    public async Task CreateBranchCondition(string methodName, string condition)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (m:Method {name: $methodName}) " +
            "MERGE (c:Condition {expression: $condition}) " +
            "MERGE (m)-[:HAS_CONDITION]->(c)",
            new { methodName, condition });
    }

    public async Task CreateExceptionHandler(string methodName, string exceptionType)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (m:Method {name: $methodName}) " +
            "MERGE (e:Exception {name: $exceptionType}) " +
            "MERGE (m)-[:CATCHES]->(e)",
            new { methodName, exceptionType });
    }

    public async Task AddBranchingComplexity(string methodName, int branchCount)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MATCH (m:Method {name: $methodName}) " +
            "SET m.branchCount = $branchCount",
            new { methodName, branchCount });
    }

    public async Task CreateInterfaceImplementation(string className, string interfaceName)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (i:Interface {name: $interfaceName}) " +
            "MERGE (c:Class {name: $className}) " +
            "MERGE (c)-[:IMPLEMENTS]->(i)",
            new { className, interfaceName });
    }

    public async Task CreateMethodReturnType(string methodName, string returnType)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (m:Method {name: $methodName}) " +
            "MERGE (t:Type {name: $returnType}) " +
            "MERGE (m)-[:RETURNS]->(t)",
            new { methodName, returnType });
    }

    public async Task CreateMethodParameter(string methodName, string paramType)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (m:Method {name: $methodName}) " +
            "MERGE (t:Type {name: $paramType}) " +
            "MERGE (m)-[:HAS_PARAMETER]->(t)",
            new { methodName, paramType });
    }


    public async Task TagGodClassAsync(string fullName)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MATCH (c {name: $fullName}) SET c:GodClass",
            new { fullName }
        );
    }

    public async Task CreateDiagnosticIssue(string className, string diagnosticId, string message, string severity, string category)
    {
        //Console.WriteLine($"    Creating diagnostic issue {diagnosticId} for {className}");
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "MERGE (d:Diagnostic {id: $diagnosticId, message: $message, severity: $severity, category: $category}) " +
            "WITH d " +
            "MATCH (c:Class {name: $className}) " +
            "MERGE (c)-[:HAS_ISSUE]->(d)",
            new { className, diagnosticId, message, severity, category });
    }

    public async Task CreateVectorIndex()
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            @"CREATE VECTOR INDEX class_embedding IF NOT EXISTS
          FOR (c:Class)
          ON c.embedding
          OPTIONS {indexConfig: {
            `vector.dimensions`: 384,
            `vector.similarity_function`: 'cosine'
          }}");
    }
}