from sentence_transformers import SentenceTransformer, util

# Load pretrained SentenceTransformer model
model = SentenceTransformer("all-MiniLM-L6-v2")

# Define labels and their mapped scores
sentiment_labels = ["positive", "negative", "neutral"]
sentiment_scores = {"positive": 1, "negative": -1, "neutral": 0}

# Pre-encode the labels
label_embeddings = model.encode(sentiment_labels, convert_to_tensor=True)

def analyze_feedback(feedback: str) -> int:
    # Encode feedback
    feedback_embedding = model.encode(feedback, convert_to_tensor=True)
    
    # Compute cosine similarity with each label
    similarities = util.cos_sim(feedback_embedding, label_embeddings)[0]
    
    # Pick best label
    best_match = int(similarities.argmax())
    label = sentiment_labels[best_match]
    
    # Return mapped score
    return sentiment_scores[label]
