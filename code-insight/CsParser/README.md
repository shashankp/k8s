
## Run
```
docker pull neo4j
dotnet restore && dotnet build && dotnet run Sample.sln
```

## Cypher clear data
```
MATCH (n)
DETACH DELETE n

:use system
CREATE OR REPLACE DATABASE neo4j
```