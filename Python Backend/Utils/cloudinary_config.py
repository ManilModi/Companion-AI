# cloudinary_config.py
import cloudinary
import cloudinary.uploader
import os
from dotenv import load_dotenv
load_dotenv()

cloudinary.config(
    cloud_name= os.getenv("CLOUDINARY_CLOUD_NAME"),
    api_key= os.getenv("CLOUDINARY_API_KEY"),
    api_secret= os.getenv("CLOUDINARY_API_SECRET"),
    secure=True
)



def upload_to_cloudinary(file_path: str, folder: str = "Mock-Interview") -> str:
    """
    Uploads a file to Cloudinary and returns the secure URL.
    """
    response = cloudinary.uploader.upload(file_path, folder=folder, resource_type="auto")
    return response.get("secure_url")
