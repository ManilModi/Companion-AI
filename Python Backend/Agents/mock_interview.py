# mock_interview.py
import os
import time
import cv2
import numpy as np
import sounddevice as sd
import scipy.io.wavfile as wav
import pyttsx3
import json
import ast
import librosa
import mediapipe as mp
import queue
from dotenv import load_dotenv
from typing import List, Dict, Any
from langchain import PromptTemplate
from langchain_groq import ChatGroq
from langchain.schema import HumanMessage
from sklearn.metrics.pairwise import cosine_similarity
from sentence_transformers import SentenceTransformer, util
import sys
import os
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '../Utils')))

from cloudinary_config import upload_to_cloudinary, cloudinary


load_dotenv()

# ---------------- Config / LLM ----------------
os.environ["GROQ_API_KEY"] = os.getenv("GROQ_API_KEY")
llm = ChatGroq(model="openai/gpt-oss-120b", api_key=os.getenv("GROQ_API_KEY"))
embeddings_model =  SentenceTransformer('all-MiniLM-L6-v2')

# ---------------- Transcription backend selection ----------------
_USE_FASTER_WHISPER = False
_USE_OPENAI_WHISPER = False
whisper_model = None

try:
    from faster_whisper import WhisperModel
    whisper_model = WhisperModel("base", device="cpu")
    _USE_FASTER_WHISPER = True
    print("[INFO] Using faster-whisper for transcription.")
except Exception:
    try:
        import whisper as openai_whisper
        whisper_model = openai_whisper.load_model("base")
        _USE_OPENAI_WHISPER = True
        print("[INFO] Using openai-whisper for transcription.")
    except Exception:
        whisper_model = None
        print("[WARN] No whisper backend available.")

# ---------------- Settings ----------------
MAX_QUESTIONS = 5
SILENCE_DURATION = 30
NEXT_QUESTION_SILENCE = 15
fs = 44100
answer_output_dir = "answers"
os.makedirs(answer_output_dir, exist_ok=True)

# ---------------- Prompt templates ----------------
question_prompt_template = PromptTemplate.from_template("""
You have to ask technical questions only.

Given the following job description, generate exactly {n} unique, distinct, and non-repetitive mock interview questions.

Format as:
["Q1","Q2",...]
Job Description:
\"\"\"{job_desc}\"\"\"
""")

answer_prompt_template = PromptTemplate.from_template("""
You are an expert technical interviewer.

Generate a concise, ideal answer for the following question:

Question:
\"\"\"{question}\"\"\"
Format as a single paragraph, clear and precise.
""")

# ---------------- JSON helper ----------------
def json_safe(obj):
    if isinstance(obj, (np.floating, np.float32, np.float64)):
        return float(obj)
    if isinstance(obj, (np.integer, np.int32, np.int64)):
        return int(obj)
    if isinstance(obj, np.ndarray):
        return obj.tolist()
    return obj

# ---------------- Question generation ----------------
def generate_questions_and_answers(job_desc: str, n: int = 5) -> List[Dict[str, str]]:
    prompt = question_prompt_template.format(job_desc=job_desc, n=n)
    response = llm.invoke([HumanMessage(content=prompt)])
    print("[DEBUG] Raw LLM Output:", response.content)
    text = response.content.strip()
    try:
        start = text.find("[")
        list_text = text[start:]
        questions = ast.literal_eval(list_text)
    except Exception:
        questions = [f"Question {i+1}" for i in range(n)]

    qa_list = []
    for q in questions:
        ans_prompt = answer_prompt_template.format(question=q)
        ans_resp = llm.invoke([HumanMessage(content=ans_prompt)])
        model_answer = ans_resp.content.strip()
        qa_list.append({"question": q, "model_answer": model_answer})
    return qa_list

# ---------------- TTS ----------------
def speak(text: str):
    engine = pyttsx3.init()
    engine.say(text)
    engine.runAndWait()

# ---------------- Recording (audio + video) ----------------
def record_av_until_silence(base: str,
                            threshold: float = 0.01,
                            silence_sec: float = SILENCE_DURATION,
                            next_q_silence: float = NEXT_QUESTION_SILENCE):
    audio_file = f"{base}_audio.wav"
    video_file = f"{base}_video.avi"

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        raise RuntimeError("Unable to open webcam.")

    frame_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    frame_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    out = cv2.VideoWriter(video_file, cv2.VideoWriter_fourcc(*'XVID'), 20.0, (frame_w, frame_h))

    q_audio = queue.Queue()
    silence_counter = 0.0
    audio_chunks = []

    def audio_callback(indata, frames, time, status):
        if status:
            print(status)
        q_audio.put(indata.copy())

    print(f"[INFO] Recording started — press 'q' to stop early.")
    with sd.InputStream(samplerate=fs, channels=1, dtype='float32', callback=audio_callback):
        try:
            while True:
                ret, frame = cap.read()
                if not ret:
                    break
                out.write(frame)
                cv2.imshow('Recording (press q to stop)', frame)

                try:
                    chunk = q_audio.get(timeout=0.1)
                    audio_chunks.append(chunk)
                    rms = float(np.sqrt(np.mean(chunk**2)))
                except queue.Empty:
                    rms = 0.0

                if rms < threshold:
                    silence_counter += 0.1
                else:
                    silence_counter = 0.0

                if silence_counter >= silence_sec or silence_counter >= next_q_silence:
                    break
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break
        finally:
            cap.release()
            out.release()
            cv2.destroyAllWindows()

    if len(audio_chunks) == 0:
        audio_array = np.zeros(int(fs * 1.0), dtype=np.float32)
    else:
        audio_array = np.concatenate(audio_chunks, axis=0).flatten()

    max_abs = float(np.max(np.abs(audio_array))) if audio_array.size > 0 else 0.0
    if max_abs > 0:
        audio_array = audio_array / max_abs
    int16_audio = np.int16(np.clip(audio_array, -1.0, 1.0) * 32767)
    wav.write(audio_file, fs, int16_audio)
    return audio_file, video_file

# ---------------- Transcription ----------------
def transcribe_audio_whisper(path: str) -> str:
    global whisper_model, _USE_FASTER_WHISPER, _USE_OPENAI_WHISPER
    if _USE_FASTER_WHISPER and whisper_model:
        try:
            segments, _ = whisper_model.transcribe(path)
            return " ".join([seg.text for seg in segments]).strip()
        except: return ""
    elif _USE_OPENAI_WHISPER and whisper_model:
        try:
            result = whisper_model.transcribe(path)
            return result.get("text", "").strip()
        except: return ""
    return ""

# ---------------- Video analysis ----------------
mp_face = mp.solutions.face_mesh
def analyze_video(path: str) -> Dict[str, float]:
    cap = cv2.VideoCapture(path)
    face_mesh = mp_face.FaceMesh(static_image_mode=False, max_num_faces=1)
    eye_scores, smile_scores = [], []

    while True:
        ret, frame = cap.read()
        if not ret: break
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = face_mesh.process(rgb)
        if results.multi_face_landmarks:
            lm = results.multi_face_landmarks[0]
            left_eye = [lm.landmark[i] for i in (33, 133)]
            right_eye = [lm.landmark[i] for i in (362, 263)]
            left_ratio = abs(left_eye[0].x - left_eye[1].x)
            right_ratio = abs(right_eye[0].x - right_eye[1].x)
            eye_scores.append(max(0.0, 1.0 - (left_ratio + right_ratio) / 0.2))
            mouth = [lm.landmark[i] for i in (61, 291)]
            smile_scores.append(abs(mouth[0].x - mouth[1].x)*10.0)
    cap.release()
    face_mesh.close()
    eye = float(np.clip(np.mean(eye_scores) if eye_scores else 0.0, 0.0, 1.0))
    smile = float(np.clip(np.mean(smile_scores) if smile_scores else 0.0, 0.0, 1.0))
    confidence = float(np.clip((eye + smile)/2.0,0.0,1.0))
    return {"eye_contact_ratio": round(eye,2),"facial_expression_score": round(smile,2),"confidence_score": round(confidence,2)}

# ---------------- Audio analysis ----------------
def analyze_audio(path: str) -> Dict[str, float]:
    y, sr = librosa.load(path, sr=fs)
    rms = librosa.feature.rms(y=y)[0]
    energy = float(np.mean(rms)) if rms.size > 0 else 0.0
    silence_ratio = float(np.sum(rms<0.01)/len(rms)) if rms.size>0 else 1.0
    try:
        f0, _, _ = librosa.pyin(y, fmin=75, fmax=400)
        pitch = float(np.nanmean(f0)) if f0 is not None and not np.all(np.isnan(f0)) else 0.0
    except:
        pitch = 0.0
    zcr = librosa.feature.zero_crossing_rate(y=y)[0]
    speaking_rate_bpm = float(np.mean(zcr)*60.0*sr/512.0)
    return {"average_energy": round(energy,4),"pitch_estimate": round(pitch,2),
            "silence_ratio": round(silence_ratio,4),"speaking_rate_bpm": round(speaking_rate_bpm,2)}

# ---------------- Similarity scoring ----------------
def compute_answer_similarity(candidate_answer: str, model_answer: str) -> float:
    candidate_vec = embeddings_model.encode(candidate_answer, convert_to_tensor=True)
    model_vec = embeddings_model.encode(model_answer, convert_to_tensor=True)
    return float(util.pytorch_cos_sim(candidate_vec, model_vec))


# ---------------- Final scoring with answer ----------------
def compute_final_score_with_answer(similarity_score: float, video: Dict[str,float], audio: Dict[str,float]) -> Dict[str,float]:
    eye = video.get("eye_contact_ratio",0.0)
    exp = video.get("facial_expression_score",0.0)
    conf = video.get("confidence_score",0.0)
    energy = audio.get("average_energy",0.0)
    silence = audio.get("silence_ratio",1.0)
    bpm = audio.get("speaking_rate_bpm",0.0)

    video_score = (eye + exp + conf)/3.0
    audio_fluency = energy*0.4 + (1.0-silence)*0.3 + (min(bpm,200)/160.0)*0.3
    audio_fluency = float(max(0.0,min(audio_fluency,1.0)))
    audio_video_score = (video_score*0.6 + audio_fluency*0.4)

    final_score = 0.5*similarity_score + 0.5*audio_video_score
    return {"similarity_score": round(similarity_score,2),
            "audio_video_score": round(audio_video_score,2),
            "final_score": round(final_score,2)}

# ---------------- Main interview flow ----------------
def run_mock_interview(job_description: str):
    job_desc = job_description.strip()
    if not job_desc:
        raise ValueError("Job description cannot be empty.")

    qa_list = generate_questions_and_answers(job_desc, MAX_QUESTIONS)
    qa_results = []

    for i, qa in enumerate(qa_list, start=1):
        q, model_ans = qa["question"], qa["model_answer"]
        print(f"\n=== Question {i} ===\n{q}")
        speak(q)
        base = os.path.join(answer_output_dir, f"q{i}")
        audio_path, video_path = record_av_until_silence(base)
        candidate_answer = transcribe_audio_whisper(audio_path).strip()
        if not candidate_answer: candidate_answer = ""
        video_results = analyze_video(video_path)
        audio_results = analyze_audio(audio_path)
        similarity_score = compute_answer_similarity(candidate_answer, model_ans) if candidate_answer else 0.0
        final_scores = compute_final_score_with_answer(similarity_score, video_results, audio_results)

        # Upload to Cloudinary
        audio_url = upload_to_cloudinary(audio_path)
        video_url = upload_to_cloudinary(video_path)

        qa_results.append({
            "question": q,
            "model_answer": model_ans,
            "candidate_answer": candidate_answer,
            "similarity_score": similarity_score,
            "video_analysis": video_results,
            "audio_analysis": audio_results,
            "final_scores": final_scores,
            "audio_url": audio_url,
            "video_url": video_url
        })

        with open(os.path.join(answer_output_dir, "qa_results_partial.json"), "w", encoding="utf-8") as pf:
            json.dump(qa_results, pf, indent=2, default=json_safe)

    # Compute overall scores as before...
    answered = [r for r in qa_results if r.get("candidate_answer")]
    avg_similarity = float(np.mean([r["similarity_score"] for r in answered])) if answered else 0.0
    avg_video_confidence = float(np.mean([r["video_analysis"]["confidence_score"] for r in answered])) if answered else 0.0
    avg_audio_energy = float(np.mean([r["audio_analysis"]["average_energy"] for r in answered])) if answered else 0.0

    final_overall = compute_final_score_with_answer(
        avg_similarity,
        {"eye_contact_ratio": avg_video_confidence, "facial_expression_score": avg_video_confidence, "confidence_score": avg_video_confidence},
        {"average_energy": avg_audio_energy, "silence_ratio":0.2, "speaking_rate_bpm":100.0}
    )

    out = {"qa_results": qa_results, "final_overall": final_overall}
    out_path = os.path.join(answer_output_dir, "qa_results.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2, default=json_safe)

    return out
if __name__ == "__main__":
    run_mock_interview("Software Engineer with Python and Machine Learning experience.")
