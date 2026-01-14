using Neo4j.Driver;
namespace CsParser;
public class GraphDbService
{
    private readonly IDriver _driver;
    public GraphDbService(IDriver driver)
    {
        _driver = driver;
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

    public async Task CreateRecordNodeAsync(string name, string nameSpace, string code)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "CREATE (n:Record {name: $name, namespace: $nameSpace, code: $code})",
            new { name, nameSpace, code }
        );
        Console.WriteLine($"Record: {name}");
    }

    public async Task CreateClassNodeAsync(string name, string nameSpace, string code)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(
            "CREATE (n:Class {name: $name, namespace: $nameSpace, code: $code})",
            new { name, nameSpace, code }
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
}