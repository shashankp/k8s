
## Run
```
dotnet restore && dotnet build && dotnet run Sample.sln
```

## Cypher clear data
```
MATCH (n)
DETACH DELETE n

:use system
CREATE OR REPLACE DATABASE neo4j
```

## Later
Logging statements - where errors/warnings are logged
Async/await patterns - to understand async call chains
Namespace relationships - group related classes
Loop structures - which methods have loops
Return points - multiple exit points in a method
Unreachable code - code that never executes