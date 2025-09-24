import os
import requests
from typing import TypedDict, List
from pydantic import BaseModel
from fastapi import FastAPI, HTTPException
from langgraph.graph import StateGraph, END
from dotenv import load_dotenv

load_dotenv()

SERPER_API_KEY = os.getenv("SERPER_API_KEY")
SERPER_API_URL = "https://google.serper.dev/search"

app = FastAPI()

# --------- 1. State Definition ----------
class JobSearchState(TypedDict):
    query: str
    raw_results: List[dict]
    formatted_jobs: List[dict]


# --------- 2. Nodes ----------

def inject_query(state: JobSearchState):
    # Just take whatever query came from input
    query = state["query"]
    print("Search Query:", query)
    return {"query": query}

def serper_search(state: JobSearchState):
    query = state["query"]
    all_results = []

    headers = {"X-API-KEY": SERPER_API_KEY, "Content-Type": "application/json"}

    for page in range(1):  # keep it short, only 1 page
        payload = {
            "q": query,
            "num": 10,
            "start": page * 10,
            "tbs": "qdr:m"
        }
        try:
            response = requests.post(SERPER_API_URL, json=payload, headers=headers)
            if response.status_code != 200:
                continue
            results = response.json()
            all_results.extend(results.get("organic", []))
        except Exception as e:
            print(f"Serper API error: {e}")
            continue

    # ✅ keep everything, no domain filtering
    return {"raw_results": all_results}

def format_results(state: JobSearchState):
    jobs = []
    for item in state["raw_results"]:
        link = item.get("link", "")
        snippet = item.get("snippet", "")
        title = item.get("title", "")

        company, location = "", ""
        snippet_parts = snippet.split("-")
        if len(snippet_parts) >= 2:
            company = snippet_parts[0].strip()
            location = snippet_parts[1].strip()

        # ❌ Remove over-filtering
        jobs.append({
            "title": title,
            "company": company,
            "location": location,
            "link": link,
            "snippet": snippet
        })

    return {"formatted_jobs": jobs}



# --------- 3. Build Graph ----------
graph = StateGraph(JobSearchState)

graph.add_node("inject_query", inject_query)
graph.add_node("serper_search", serper_search)
graph.add_node("format_results", format_results)

graph.set_entry_point("inject_query")

graph.add_edge("inject_query", "serper_search")
graph.add_edge("serper_search", "format_results")
graph.add_edge("format_results", END)

job_search_agent = graph.compile()

