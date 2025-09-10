import os
import json
import fitz  # PyMuPDF
from docx import Document
from typing import TypedDict

from langchain_core.prompts import PromptTemplate
from langchain_core.output_parsers import StrOutputParser
from langchain_groq import ChatGroq
from langgraph.graph import StateGraph, END


from dotenv import load_dotenv
load_dotenv()

# --------- 1. Resume parsing utilities ----------
def extract_text_from_pdf(path):
    try:
        doc = fitz.open(path)
        return "\n".join([page.get_text() for page in doc])
    except Exception as e:
        print(f"Failed to read PDF {path}: {e}")
        return None


def contains_image(pdf_path):
    try:
        doc = fitz.open(pdf_path)
        for page in doc:
            if page.get_images(full=True):
                return True
        return False
    except Exception as e:
        print(f"Failed to check images in {pdf_path}: {e}")
        return False


def image_resume_parsing(pdf_path):
    from unstructured.partition.pdf import partition_pdf
    elements = partition_pdf(
        filename=pdf_path,
        extract_images_in_pdf=True,
        ocr_languages="eng",
        strategy="hi_res"
    )
    return "\n".join([str(el) for el in elements])


def extract_text_from_docx(path):
    try:
        doc = Document(path)
        return "\n".join([p.text for p in doc.paragraphs])
    except Exception as e:
        print(f"Failed to read DOCX {path}: {e}")
        return None


# --------- 2. LangGraph State ----------
class ResumeState(TypedDict):
    resume_file_path: str
    file_type: str
    resume_text: str
    structured_output: dict


# --------- 3. LLM Setup ----------
llm = ChatGroq(
    model_name="openai/gpt-oss-120b",
    api_key=os.getenv("GROQ_API_KEY")
)

prompt_template = PromptTemplate.from_template("""
You are an expert at extracting structured JSON from resumes.

ONLY RETURN VALID JSON. Do not include any explanation or text outside of JSON.

Extract the following details:

{{
  "name": "string",
  "contact_no": "string",
  "email": "string",
  "linkedin_profile_link": "string",
  "skills": ["skill1", "skill2"],
  "experience": "string",
  "total_experience_years": float,
  "projects_built": ["project1", "project2"],
  "achievements_like_awards_and_certifications": ["achievement1"]
}}

--- START OF RESUME ---
{resume_text}
--- END OF RESUME ---
""")

parser = StrOutputParser()


# --------- 4. Define Graph Nodes ----------
def detect_file_type(state: ResumeState):
    path = state["resume_file_path"]
    if path.endswith(".pdf"):
        if contains_image(path):
            return {"file_type": "image_pdf"}
        return {"file_type": "text_pdf"}
    elif path.endswith(".docx"):
        return {"file_type": "docx"}
    else:
        return {"file_type": "unsupported"}


def parse_text_pdf(state: ResumeState):
    text = extract_text_from_pdf(state["resume_file_path"])
    return {"resume_text": text or ""}


def parse_image_pdf(state: ResumeState):
    text = image_resume_parsing(state["resume_file_path"])
    return {"resume_text": text or ""}


def parse_docx_file(state: ResumeState):
    text = extract_text_from_docx(state["resume_file_path"])
    return {"resume_text": text or ""}


def handle_unsupported(state: ResumeState):
    return {"structured_output": {"error": "Unsupported file type"}}


def extract_structured_json(state: ResumeState):
    if not state.get("resume_text"):
        return {"structured_output": {"error": "No resume text"}}

    chain = prompt_template | llm | parser
    result = chain.invoke({"resume_text": state["resume_text"]})

    try:
        structured = json.loads(result)
    except:
        structured = {"error": "Invalid JSON returned", "raw": result}

    return {"structured_output": structured}


# --------- 5. Build LangGraph Workflow ----------
graph = StateGraph(ResumeState)

graph.add_node("detect_file_type", detect_file_type)
graph.add_node("parse_text_pdf", parse_text_pdf)
graph.add_node("parse_image_pdf", parse_image_pdf)
graph.add_node("parse_docx_file", parse_docx_file)
graph.add_node("handle_unsupported", handle_unsupported)
graph.add_node("extract_structured_json", extract_structured_json)

# entry
graph.set_entry_point("detect_file_type")

# branching
graph.add_conditional_edges(
    "detect_file_type",
    lambda state: state["file_type"],
    {
        "text_pdf": "parse_text_pdf",
        "image_pdf": "parse_image_pdf",
        "docx": "parse_docx_file",
        "unsupported": "handle_unsupported"
    },
)

# edges
graph.add_edge("parse_text_pdf", "extract_structured_json")
graph.add_edge("parse_image_pdf", "extract_structured_json")
graph.add_edge("parse_docx_file", "extract_structured_json")
graph.add_edge("handle_unsupported", END)
graph.add_edge("extract_structured_json", END)

resume_agent = graph.compile()


# --------- 6. Usage ----------
if __name__ == "__main__":
    result = resume_agent.invoke({"resume_file_path": "../Resumes/Ajay_Pawar_5year_ sr Angular developer .docx"})
    print(json.dumps(result["structured_output"], indent=2))
