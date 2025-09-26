import os
from langchain_groq import ChatGroq
from langchain_core.messages import HumanMessage
from dotenv import load_dotenv
load_dotenv()

# Make sure your env has GROQ_API_KEY set
# Example: set it in .env or run: export GROQ_API_KEY="your_key"

def test_chatgroq():
    llm = ChatGroq(
        model="openai/gpt-oss-120b",
        api_key=os.getenv("GROQ_API_KEY")
    )

    response = llm.invoke([
        HumanMessage(content="Give me a short fun fact about space.")
    ])

    print("Response from gpt-oss-120b:")
    print(response.content)

if __name__ == "__main__":
    test_chatgroq()
