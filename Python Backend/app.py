import os
import shutil
import json
from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse

from Agents.resume_agent import resume_agent  # import your LangGraph pipeline

app = FastAPI(title="Resume Parser API", version="1.0")


@app.post("/parse-resume/")
async def parse_resume(file: UploadFile = File(...)):
    """
    Upload a resume (PDF/DOCX) and get structured JSON response.
    """

    # Validate file type
    if not (file.filename.endswith(".pdf") or file.filename.endswith(".docx")):
        raise HTTPException(status_code=400, detail="Only PDF or DOCX files are supported")

    # Save temporarily
    temp_dir = "temp_uploads"
    os.makedirs(temp_dir, exist_ok=True)
    file_path = os.path.join(temp_dir, file.filename)

    with open(file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    try:
        # Run the resume agent
        result = resume_agent.invoke({"resume_file_path": file_path})
        return JSONResponse(content=result["structured_output"])
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Resume parsing failed: {e}")
    finally:
        # Cleanup file
        if os.path.exists(file_path):
            os.remove(file_path)
