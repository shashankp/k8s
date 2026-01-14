from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp import types
from neo4j import GraphDatabase
from sentence_transformers import SentenceTransformer
from dotenv import load_dotenv
from pydantic import BaseModel
import os

class SearchRequest(BaseModel):
    question: str
    top_k: int = 5

load_dotenv()

neo4j_uri = os.getenv("NEO4J_URI", "bolt://localhost:7687")
neo4j_user = os.getenv("NEO4J_USER", "neo4j")
neo4j_password = os.getenv("NEO4J_PASSWORD")
driver = GraphDatabase.driver(neo4j_uri, auth=(neo4j_user, neo4j_password))

app = Server("codeinsight")
model = SentenceTransformer('all-MiniLM-L6-v2')
@app.list_tools()
async def list_tools() -> list[types.Tool]:
    return [
        types.Tool(
            name="search_code",
            description="Search for code using semantic similarity",
            inputSchema={
                "type": "object",
                "properties": {
                    "question": {"type": "string"},
                    "top_k": {"type": "integer", "default": 5}
                },
                "required": ["question"]
            }
        ),
        types.Tool(
            name="get_node",
            description="Get details of a specific class, record, or type by name",
            inputSchema={
                "type": "object",
                "properties": {
                    "node_name": {"type": "string"}
                },
                "required": ["node_name"]
            }
        ),
        types.Tool(
            name="get_relationships",
            description="Find what a node depends on, inherits from, calls, or issues it has",
            inputSchema={
                "type": "object",
                "properties": {
                    "node_name": {"type": "string"}
                },
                "required": ["node_name"]
            }
        ),
        types.Tool(
            name="get_neighbors",
            description="Explore connected nodes up to a certain depth with relationship types",
            inputSchema={
                "type": "object",
                "properties": {
                    "node_name": {"type": "string"},
                    "depth": {"type": "integer", "default": 1}
                },
                "required": ["node_name"]
            }
        )
    ]

@app.call_tool()
async def call_tool(name: str, arguments: dict) -> list[types.TextContent]:
    if name == "search_code":
        question = arguments["question"]
        top_k = arguments.get("top_k", 5)
        
        question_embedding = model.encode(question).tolist()
        
        with driver.session() as session:
            result = session.run(
                """
                CALL db.index.vector.queryNodes('class_embedding', $topK, $questionEmbedding)
                YIELD node, score
                RETURN node.name as name, node.code as code, node.filePath as filePath, score
                """,
                topK=top_k,
                questionEmbedding=question_embedding
            )
            
            results = [{"name": r["name"], "code": r["code"], "filePath": r["filePath"], "score": r["score"]} 
                       for r in result]
        
        return [types.TextContent(type="text", text=str(results))]
    elif name == "get_node":
        node_name = arguments["node_name"]
        
        with driver.session() as session:
            result = session.run(
                "MATCH (n) WHERE n.name = $name RETURN n",
                name=node_name
            )
            record = result.single()
            if record:
                node = dict(record["n"])
                return [types.TextContent(type="text", text=str(node))]
            return [types.TextContent(type="text", text="Node not found")]

    elif name == "get_relationships":
        node_name = arguments["node_name"]
        
        with driver.session() as session:
            result = session.run(
                """
                MATCH (n)-[r]->(m) WHERE n.name = $name
                RETURN type(r) as relationship, m.name as target, labels(m) as targetType
                """,
                name=node_name
            )
            relationships = [{"type": r["relationship"], "target": r["target"], "targetType": r["targetType"]} 
                            for r in result]
        
        return [types.TextContent(type="text", text=str(relationships))]
    elif name == "get_neighbors":
        node_name = arguments["node_name"]
        depth = arguments.get("depth", 1)
        
        with driver.session() as session:
            result = session.run(
                f"""
                MATCH path = (n)-[r*1..{depth}]-(m) WHERE n.name = $name
                RETURN DISTINCT m.name as neighbor, labels(m) as type, 
                    [rel in r | type(rel)] as relationships
                """,
                name=node_name
            )
            neighbors = [{"name": r["neighbor"], "type": r["type"], "relationships": r["relationships"]} 
                        for r in result]
        
        return [types.TextContent(type="text", text=str(neighbors))]

async def main():
    async with stdio_server() as (read_stream, write_stream):
        await app.run(
            read_stream,
            write_stream,
            app.create_initialization_options()
        )

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())