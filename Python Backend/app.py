import os
import shutil
import json
from fastapi import FastAPI, UploadFile, File, HTTPException, Form
from fastapi.responses import JSONResponse, StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Dict, Any, List, Optional
from Feedback.sentiment import analyze_feedback


from Agents.resume_agent import resume_agent
from Agents.scoring_agent import scoring_agent
from Agents.JobSearch_agent import job_search_agent
from Agents.mock_interview import run_mock_interview



app = FastAPI(title="Resume + Scoring API", version="1.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
def health():
    return {"status": "ok"}


@app.post("/parse-resume/")
async def parse_resume(file: UploadFile = File(...)):
    if not (file.filename.endswith(".pdf") or file.filename.endswith(".docx")):
        raise HTTPException(status_code=400, detail="Only PDF or DOCX files are supported")

    temp_dir = "temp_uploads"
    os.makedirs(temp_dir, exist_ok=True)
    file_path = os.path.join(temp_dir, file.filename)

    try:
        with open(file_path, "wb") as f_out:
            shutil.copyfileobj(file.file, f_out)

        parsed = resume_agent.invoke({"resume_file_path": file_path})

        if isinstance(parsed, str):
            try:
                parsed = json.loads(parsed)
            except:
                return JSONResponse(status_code=500, content={"error": "Failed to parse resume output."})

        return JSONResponse(content=parsed.get("structured_output", parsed))
    finally:
        if os.path.exists(file_path):
            os.remove(file_path)

class ScoreResumeInput(BaseModel):
    resume_json: Dict[str, Any]
    job_description: str

@app.post("/score-resume/")
async def score_resume(input_data: ScoreResumeInput):
    try:
        payload = json.dumps({
            "resume_json": input_data.resume_json,
            "job_description": input_data.job_description
        })

        result = scoring_agent(payload)

        parsed_result = result.get("parsed_result", {})
        feedback = result.get("feedback", "")
        total_score = parsed_result.get("total_score", 0)

        return JSONResponse(content={
            "score": total_score,
            "feedback": feedback,
            "raw_output": parsed_result
        })

    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Scoring failed: {e}")


class SentimentInput(BaseModel):
    feedback: str

@app.post("/analyze-feedback/")
async def analyze_feedback_endpoint(input_data: SentimentInput):
    try:
        score = analyze_feedback(input_data.feedback)
        return JSONResponse(content={
            "feedback": input_data.feedback,
            "sentiment_score": score
        })
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Sentiment analysis failed: {e}")



class TextInput(BaseModel):
    text: str

class EmbeddingResponse(BaseModel):
    embedding: List[float]


model = None

def get_model():
    global model
    if model is None:
        from sentence_transformers import SentenceTransformer
        model = SentenceTransformer("all-MiniLM-L6-v2")
    return model

@app.post("/embed")
async def get_embedding(data: TextInput):
    vector = get_model().encode(data.text).tolist()
    return {"embedding": vector}


@app.get("/charts")
def get_charts():
    with open("charts.json", "r") as f:
        data = json.load(f)
    return data

class ResumeJobSearchRequest(BaseModel):
    name: str
    email: str
    contact_no: str
    linkedin_profile_link: Optional[str] = ""
    skills: List[str]
    experience: str
    total_experience_years: float
    projects_built: List[str]
    achievements_like_awards_and_certifications: List[str]


class JobItem(BaseModel):
    title: str
    link: str
    snippet: str

class JobSearchResponse(BaseModel):
    jobs: List[JobItem]

class CustomPromptRequest(BaseModel):
    custom_prompt: str



@app.post("/search_jobs", response_model=JobSearchResponse)
def search_jobs(request: CustomPromptRequest):
    try:
        query = request.custom_prompt
        result = job_search_agent.invoke({"query": query})
        jobs = result.get("formatted_jobs", [])
        return {"jobs": jobs}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))



class InterviewRequest(BaseModel):
    job_id: int
    job_desc: str

@app.post("/start_interview/")
async def start_interview(request: InterviewRequest):
    try:

        result = run_mock_interview(request.job_desc)
        final_overall = result.get("final_overall", {})
        qa_results = result.get("qa_results", [])

        return JSONResponse(content={
            "status": "success",
            "results": qa_results,
            "final_overall": final_overall
        })

    except Exception as e:
        return JSONResponse(
            content={"status": "error", "message": str(e)},
            status_code=500
        )