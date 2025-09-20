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
from dotenv import load_dotenv
from typing import List, Dict, Any
from langchain import PromptTemplate
from langchain_groq import ChatGroq
from langchain.schema import HumanMessage

load_dotenv()

# ---------------- Config / LLM ----------------
os.environ["GROQ_API_KEY"] = os.getenv("GROQ_API_KEY")
llm = ChatGroq(model_name="openai/gpt-oss-120b", api_key=os.getenv("GROQ_API_KEY"))

# ---------------- Transcription backend selection ----------------
# Preferred: faster-whisper (Windows-friendly and faster). Fallback: openai-whisper.
_USE_FASTER_WHISPER = False
_USE_OPENAI_WHISPER = False
whisper_model = None

try:
    from faster_whisper import WhisperModel
    whisper_model = WhisperModel("base", device="cpu")  # change device="cuda" if you have GPU
    _USE_FASTER_WHISPER = True
    print("[INFO] Using faster-whisper for transcription.")
except Exception:
    try:
        import whisper as openai_whisper  # requires "openai-whisper" pip package
        whisper_model = openai_whisper.load_model("base")
        _USE_OPENAI_WHISPER = True
        print("[INFO] Using openai-whisper for transcription.")
    except Exception:
        whisper_model = None
        print("[WARN] No whisper backend available. Install 'faster-whisper' or 'openai-whisper' for transcription.")

# ---------------- Settings ----------------
MAX_QUESTIONS = 2
SILENCE_DURATION = 30        # seconds to stop recording
NEXT_QUESTION_SILENCE = 15   # seconds of silence to move to next question
fs = 44100                   # sampling rate
answer_output_dir = "answers"
os.makedirs(answer_output_dir, exist_ok=True)

# ---------------- Prompt template ----------------
question_prompt_template = PromptTemplate.from_template("""
You have to ask technical questions only.

Given the following job description, generate exactly {n} unique, distinct, and non-repetitive mock interview questions.

Format as:
["Q1","Q2",...]
Job Description:
\"\"\"{job_desc}\"\"\"
""")

# ---------------- JSON helper ----------------
def json_safe(obj):
    # Convert numpy types and arrays to native python types
    if isinstance(obj, (np.floating, np.float32, np.float64)):
        return float(obj)
    if isinstance(obj, (np.integer, np.int32, np.int64)):
        return int(obj)
    if isinstance(obj, np.ndarray):
        return obj.tolist()
    # fallback: return as-is (json.dump with default will call this function)
    return obj

# ---------------- Question generation ----------------
def generate_questions_groq(job_desc: str, n: int = 2) -> List[str]:
    print("[INFO] Generating questions using Groq LLaMA...")
    prompt = question_prompt_template.format(job_desc=job_desc, n=n)
    response = llm.invoke([HumanMessage(content=prompt)])
    text = response.content.strip()
    try:
        start = text.find("[")
        if start == -1:
            raise ValueError("No list found in LLM output")
        list_text = text[start:]
        questions = ast.literal_eval(list_text)
        if isinstance(questions, list):
            unique_q = []
            for q in questions:
                if q not in unique_q:
                    unique_q.append(q)
            return unique_q[:n]
    except Exception as e:
        print("[WARN] Could not parse LLM output for questions:", e)
    # fallback
    return ["Tell me about yourself."] * n

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
    """
    Records video and audio in chunks until a pause is detected.
    Writes video to <base>_video.avi and audio to <base>_audio.wav (16-bit PCM).
    Returns (audio_filepath, video_filepath)
    """
    audio_file = f"{base}_audio.wav"
    video_file = f"{base}_video.avi"

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        raise RuntimeError("Unable to open webcam. Check camera permissions/drivers.")

    frame_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    frame_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    out = cv2.VideoWriter(video_file, cv2.VideoWriter_fourcc(*'XVID'), 20.0, (frame_w, frame_h))

    audio_chunks = []
    silence_counter = 0.0
    chunk_dur = 0.5
    chunk_size = int(fs * chunk_dur)

    print(f"[INFO] Recording started — press 'q' in the video window to stop early. chunk_size={chunk_size}")

    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                print("[WARN] No frame captured from camera.")
                break
            out.write(frame)
            cv2.imshow('Recording (press q to stop)', frame)

            # record audio chunk
            chunk = sd.rec(chunk_size, samplerate=fs, channels=1, dtype='float32')
            sd.wait()

            if chunk is None or chunk.size == 0:
                chunk_flat = np.zeros(chunk_size, dtype=np.float32)
            else:
                chunk_flat = np.asarray(chunk).astype(np.float32).flatten()

            # compute RMS for this chunk
            rms = float(np.sqrt(np.mean(chunk_flat**2))) if chunk_flat.size > 0 else 0.0
            audio_chunks.append(chunk_flat)

            # debug logging
            print(f"[DEBUG] chunk RMS: {rms:.6f} | silence_counter: {silence_counter:.1f}s")

            if rms < threshold:
                silence_counter += chunk_dur
            else:
                silence_counter = 0.0

            # stop conditions
            if silence_counter >= silence_sec:
                print("[INFO] Long silence detected -> stopping recording.")
                break
            if silence_counter >= next_q_silence:
                print("[INFO] Short silence detected -> moving to next question.")
                break

            # allow manual stop
            if cv2.waitKey(1) & 0xFF == ord('q'):
                print("[INFO] User requested stop (q).")
                break

    finally:
        cap.release()
        out.release()
        cv2.destroyAllWindows()

    # concatenate chunks to a single float32 array
    if len(audio_chunks) == 0:
        print("[WARN] No audio captured; writing 1s of silence.")
        audio_array = np.zeros(int(fs * 1.0), dtype=np.float32)
    else:
        audio_array = np.concatenate(audio_chunks, axis=0).astype(np.float32)

    # normalize if necessary and convert to int16 PCM
    max_abs = float(np.max(np.abs(audio_array))) if audio_array.size > 0 else 0.0
    if max_abs > 1.0:
        audio_array = audio_array / max_abs
    int16_audio = np.int16(np.clip(audio_array, -1.0, 1.0) * 32767)

    wav.write(audio_file, fs, int16_audio)
    print(f"[INFO] Saved audio -> {audio_file} (samples={len(int16_audio)})")
    print(f"[INFO] Saved video -> {video_file}")

    return audio_file, video_file

# ---------------- Transcription ----------------
def transcribe_audio_whisper(path: str) -> str:
    """
    Uses faster-whisper (preferred) or openai-whisper to transcribe the WAV file.
    Returns the transcription string ('' if nothing / failure).
    """
    global whisper_model, _USE_FASTER_WHISPER, _USE_OPENAI_WHISPER
    if _USE_FASTER_WHISPER and whisper_model is not None:
        try:
            segments, info = whisper_model.transcribe(path)
            text = " ".join([seg.text for seg in segments]).strip()
            if text == "":
                print("[WARN] faster-whisper returned empty transcription.")
            return text
        except Exception as e:
            print("[ERROR] faster-whisper transcription failed:", e)
            return ""
    elif _USE_OPENAI_WHISPER and whisper_model is not None:
        try:
            result = whisper_model.transcribe(path)
            text = result.get("text", "").strip()
            if text == "":
                print("[WARN] openai-whisper returned empty transcription.")
            return text
        except Exception as e:
            print("[ERROR] openai-whisper transcription failed:", e)
            return ""
    else:
        print("[ERROR] No transcription backend available. Install faster-whisper or openai-whisper.")
        return ""

# ---------------- Validate answer with LLM ----------------
def validate_answer_with_llm(question: str, answer: str) -> str:
    prompt = f"You are an expert technical interviewer.\nQuestion:\n{question}\n\nCandidate's answer:\n{answer}\n\nGive a short evaluation on accuracy, completeness, and relevance."
    response = llm.invoke([HumanMessage(content=prompt)])
    return response.content.strip()

# ---------------- Video analysis (MediaPipe Face Mesh) ----------------
mp_face = mp.solutions.face_mesh

def analyze_video(path: str) -> Dict[str, float]:
    cap = cv2.VideoCapture(path)
    face_mesh = mp_face.FaceMesh(static_image_mode=False, max_num_faces=1)
    eye_scores = []
    smile_scores = []

    while True:
        ret, frame = cap.read()
        if not ret:
            break
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = face_mesh.process(rgb)
        if results.multi_face_landmarks:
            lm = results.multi_face_landmarks[0]
            # simple eye width proxy
            left_eye = [lm.landmark[i] for i in (33, 133)]
            right_eye = [lm.landmark[i] for i in (362, 263)]
            left_ratio = abs(left_eye[0].x - left_eye[1].x)
            right_ratio = abs(right_eye[0].x - right_eye[1].x)
            eye_scores.append(max(0.0, 1.0 - (left_ratio + right_ratio) / 0.2))

            # mouth width proxy for smile
            mouth = [lm.landmark[i] for i in (61, 291)]
            mouth_width = abs(mouth[0].x - mouth[1].x)
            smile_scores.append(mouth_width * 10.0)

    cap.release()
    face_mesh.close()

    eye = float(np.clip(np.mean(eye_scores) if eye_scores else 0.0, 0.0, 1.0))
    smile = float(np.clip(np.mean(smile_scores) if smile_scores else 0.0, 0.0, 1.0))
    confidence = float(np.clip((eye + smile) / 2.0, 0.0, 1.0))

    return {
        "eye_contact_ratio": round(eye, 2),
        "facial_expression_score": round(smile, 2),
        "confidence_score": round(confidence, 2)
    }

# ---------------- Audio analysis (librosa) ----------------
def analyze_audio(path: str) -> Dict[str, float]:
    # librosa loads with float32
    y, sr = librosa.load(path, sr=fs)
    rms = librosa.feature.rms(y=y)[0]
    energy = float(np.mean(rms)) if rms.size > 0 else 0.0
    silence_ratio = float(np.sum(rms < 0.01) / len(rms)) if rms.size > 0 else 1.0

    # pitch using pyin
    try:
        f0, voiced_flag, voiced_probs = librosa.pyin(y, fmin=75, fmax=400)
        pitch = float(np.nanmean(f0)) if f0 is not None and not np.all(np.isnan(f0)) else 0.0
    except Exception:
        pitch = 0.0

    # speaking rate proxy from zero crossing
    zcr = librosa.feature.zero_crossing_rate(y=y)[0]
    speaking_rate_bpm = float(np.mean(zcr) * 60.0 * sr / 512.0)

    return {
        "average_energy": round(energy, 4),
        "pitch_estimate": round(pitch, 2),
        "silence_ratio": round(silence_ratio, 4),
        "speaking_rate_bpm": round(speaking_rate_bpm, 2)
    }

# ---------------- Final scoring ----------------
def compute_final_score(video: Dict[str, float], audio: Dict[str, float]) -> Dict[str, float]:
    eye = video.get("eye_contact_ratio", 0.0)
    exp = video.get("facial_expression_score", 0.0)
    conf = video.get("confidence_score", 0.0)

    energy = audio.get("average_energy", 0.0)
    silence = audio.get("silence_ratio", 1.0)
    bpm = audio.get("speaking_rate_bpm", 0.0)

    video_score = (eye + exp + conf) / 3.0
    audio_fluency = energy * 0.4 + (1.0 - silence) * 0.3 + (min(bpm, 200) / 160.0) * 0.3
    audio_fluency = float(max(0.0, min(audio_fluency, 1.0)))
    final_norm = video_score * 0.6 + audio_fluency * 0.4

    return {
        "video_score": round(video_score, 2),
        "audio_fluency_score": round(audio_fluency, 2),
        "final_score": round(final_norm, 2)
    }

# ---------------- Main interview flow ----------------
def run_mock_interview():
    # Read job description from jd.txt
    jd_path = "jd.txt"
    if not os.path.exists(jd_path):
        print(f"[ERROR] {jd_path} not found. Please create it with the job description.")
        return
    with open(jd_path, "r", encoding="utf-8") as f:
        job_desc = f.read().strip()
    if not job_desc:
        print("[ERROR] jd.txt is empty.")
        return

    questions = generate_questions_groq(job_desc, MAX_QUESTIONS)
    qa_results = []

    for i, q in enumerate(questions, start=1):
        print(f"\n=== Question {i} ===")
        print(q)
        speak(q)

        base = os.path.join(answer_output_dir, f"q{i}")
        audio_path, video_path = record_av_until_silence(base)

        # Transcribe
        transcription = transcribe_audio_whisper(audio_path).strip()
        if transcription == "":
            print("[INFO] No speech detected / transcription empty.")
        else:
            print(f"[TRANSCRIPTION] {transcription}")

        # Only call LLM evaluation if transcription not empty
        if transcription:
            evaluation = validate_answer_with_llm(q, transcription)
        else:
            evaluation = {"note": "No answer provided. Evaluation skipped.", "score": None}

        # Run analyses
        video_results = analyze_video(video_path)
        audio_results = analyze_audio(audio_path)

        qa_results.append({
            "question": q,
            "answer": transcription,
            "unanswered": not bool(transcription),
            "evaluation": evaluation,
            "video_analysis": video_results,
            "audio_analysis": audio_results
        })

        # Save intermediate results progressively (safe dump)
        with open(os.path.join(answer_output_dir, "qa_results_partial.json"), "w", encoding="utf-8") as pf:
            json.dump(qa_results, pf, indent=2, default=json_safe)

    # Compute final aggregated scores only over answered items
    answered = [r for r in qa_results if r.get("answer")]
    if answered:
        avg_video_confidence = float(np.mean([r["video_analysis"]["confidence_score"] for r in answered]))
        avg_audio_energy = float(np.mean([r["audio_analysis"]["average_energy"] for r in answered]))
    else:
        avg_video_confidence = 0.0
        avg_audio_energy = 0.0

    final = compute_final_score(
        {"eye_contact_ratio": avg_video_confidence, "facial_expression_score": avg_video_confidence, "confidence_score": avg_video_confidence},
        {"average_energy": avg_audio_energy, "silence_ratio": 0.2, "speaking_rate_bpm": 100.0}
    )

    out = {"qa_results": qa_results, "final_overall": final}
    out_path = os.path.join(answer_output_dir, "qa_results.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2, default=json_safe)

    print(f"\n[INFO] Interview complete. Results saved to {out_path}")
    print("Final overall:", final)

if __name__ == "__main__":
    run_mock_interview()
