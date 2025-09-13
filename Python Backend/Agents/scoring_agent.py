import os
import json
from langgraph.graph import StateGraph, END
from langchain_core.prompts import PromptTemplate
from langchain_core.output_parsers import StrOutputParser
from langchain_groq import ChatGroq
from langchain.tools import tool
from .resume_agent import resume_agent

# === LLM Setup ===
llm = ChatGroq(
    model_name="openai/gpt-oss-120b",
    api_key=os.getenv("GROQ_API_KEY")
)

# === Prompt Template ===
prompt_template = PromptTemplate.from_template("""
You are a recruitment expert evaluating a candidate's resume for a specific job.

Resume text:
{resume_text}

Job description:
{job_description}

Here are 1–3 past candidate evaluations for similar roles:
{previous_results}

Evaluate the current resume based on the following criteria:
- Technical Skills (30%)
- Experience (25%)
- Certifications (15%)
- Projects (15%)
- Soft Skills (15%)

Scoring Guidelines:
- Deduct points for missing skills, experience, or certifications.
- Only award high scores for strong direct matches.
- Do not assume information not present.
- Use past resumes if helpful.

Respond ONLY in JSON:
{{
  "scores": {{
    "technical_skills": int,
    "experience": int,
    "certifications": int,
    "projects": int,
    "soft_skills": int
  }},
  "total_score": int,
  "strengths_summary": str,
  "improvement_areas": [str, str, str],
  "suggestions": [str, str, str]
}}
""")

# === History Helpers ===
HISTORY_FILE = "resume_history.json"

def load_resume_history(limit=3):
    if not os.path.exists(HISTORY_FILE):
        return "[]"
    with open(HISTORY_FILE, "r") as f:
        history = json.load(f)
    return json.dumps(history[-limit:], indent=2)

def save_to_history(new_result):
    history = []
    if os.path.exists(HISTORY_FILE):
        with open(HISTORY_FILE, "r") as f:
            history = json.load(f)
    history.append(new_result)
    with open(HISTORY_FILE, "w") as f:
        json.dump(history, f, indent=2)

# === Graph State ===
class ScoringState(dict):
    resume_json: dict
    job_description: str
    parsed_result: dict
    feedback: str

# === Node: Evaluate Resume ===
def evaluate_resume(state: ScoringState):
    resume_json = state["resume_json"]
    job_description = state["job_description"]

    resume_text = f"""
Name: {resume_json.get('name')}
Skills: {', '.join(resume_json.get('skills', []))}
Experience: {resume_json.get('experience')}
Certifications: {', '.join(resume_json.get('achievements_like_awards_and_certifications', []))}
Projects: {', '.join(resume_json.get('projects_built', []))}
"""

    prior_context = load_resume_history()

    # Build chain and invoke LLM
    chain = prompt_template | llm | StrOutputParser()
    result_str = chain.invoke({
        "resume_text": resume_text,
        "job_description": job_description,
        "previous_results": prior_context
    })

    try:
        result_json = json.loads(result_str)
        save_to_history({
            "resume_snippet": resume_text[:500],
            "job_description_snippet": job_description[:500],
            "result": result_json
        })

        feedback_text = f"""
=== Candidate Feedback ===

✅ Match Score: {result_json['total_score']} / 100

📊 Scores:
- Technical Skills: {result_json['scores']['technical_skills']}
- Experience: {result_json['scores']['experience']}
- Certifications: {result_json['scores']['certifications']}
- Projects: {result_json['scores']['projects']}
- Soft Skills: {result_json['scores']['soft_skills']}

🌟 Strengths:
{result_json['strengths_summary']}

🔧 Areas to Improve:
- {result_json['improvement_areas'][0]}
- {result_json['improvement_areas'][1]}
- {result_json['improvement_areas'][2]}

📚 Suggestions:
- {result_json['suggestions'][0]}
- {result_json['suggestions'][1]}
- {result_json['suggestions'][2]}
""".strip()

        state["parsed_result"] = result_json
        state["feedback"] = feedback_text

    except Exception as e:
        state["parsed_result"] = {}
        state["feedback"] = f"Error parsing LLM output: {e}\nRaw:\n{result_str}"

    return state

# === Build Graph ===
workflow = StateGraph(ScoringState)
workflow.add_node("evaluate_resume", evaluate_resume)
workflow.set_entry_point("evaluate_resume")
workflow.add_edge("evaluate_resume", END)

scoring_graph = workflow.compile()

# === Tool Wrapper ===
@tool
def scoring_agent(input_str: str) -> dict:
    """
    input_str: JSON string with keys 'resume_json' and 'job_description'
    Returns dict with parsed_result and feedback
    """
    data = json.loads(input_str)
    resume_json = data.get("resume_json", {})
    job_description = data.get("job_description", "")

    result = scoring_graph.invoke({
        "resume_json": resume_json,
        "job_description": job_description
    })
    return {
        "parsed_result": result.get("parsed_result", {}),
        "feedback": result.get("feedback", "")
    }
