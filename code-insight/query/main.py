from fastapi import FastAPI
from neo4j import GraphDatabase
from dotenv import load_dotenv
import os

load_dotenv()

neo4j_uri = os.getenv("NEO4J_URI", "bolt://localhost:7687")
neo4j_user = os.getenv("NEO4J_USER", "neo4j")
neo4j_password = os.getenv("NEO4J_PASSWORD")
driver = GraphDatabase.driver(neo4j_uri, auth=(neo4j_user, neo4j_password))

app = FastAPI()

@app.get("/")
def read_root():
    return {"message": "CodeInsight API"}
