import os
import shutil
import json
from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Dict, Any
from Feedback.sentiment import analyze_feedback

from Agents.resume_agent import resume_agent
from Agents.scoring_agent import scoring_agent

app = FastAPI(title="Resume + Scoring API", version="1.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# === Resume Parsing Endpoint ===
@app.post("/parse-resume/")
async def parse_resume(file: UploadFile = File(...)):
    if not (file.filename.endswith(".pdf") or file.filename.endswith(".docx")):
        raise HTTPException(status_code=400, detail="Only PDF or DOCX files are supported")

    temp_dir = "temp_uploads"
    os.makedirs(temp_dir, exist_ok=True)
    file_path = os.path.join(temp_dir, file.filename)

    try:
        # Save uploaded file
        with open(file_path, "wb") as f_out:
            shutil.copyfileobj(file.file, f_out)

        parsed = resume_agent.invoke({"resume_file_path": file_path})

        # Ensure we return dict
        if isinstance(parsed, str):
            try:
                parsed = json.loads(parsed)
            except:
                return JSONResponse(status_code=500, content={"error": "Failed to parse resume output."})

        return JSONResponse(content=parsed.get("structured_output", parsed))
    finally:
        if os.path.exists(file_path):
            os.remove(file_path)


# === Resume Scoring Endpoint ===
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

# === Sentiment Analysis Endpoint ===
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
