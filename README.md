# Companion-AI - AI-Powered Hiring Platform

Revolutionize your hiring process with AI-powered recruitment solutions that streamline talent acquisition and help companies find the best candidates faster.

## 🚀 Features

### For HR Professionals
- **AI-Powered Resume Screening**: Automated resume parsing and candidate scoring
- **Intelligent Job Posting**: Create comprehensive job descriptions with built-in aptitude tests
- **Smart Candidate Matching**: Advanced algorithms to match candidates with job requirements
- **Interview Scheduling**: Automated communication and interview coordination
- **Application Management**: Comprehensive dashboard for tracking all applications
- **User Authentication with OTP (Email)**: Secure login and verification using one-time passwords sent via SMTP

### For Candidates
- **Job Insights & Analytics**: Detailed market analysis and career recommendations
- **Skill Assessment**: Interactive aptitude tests and technical evaluations
- **Resume Optimization**: AI-powered feedback and improvement suggestions
- **Career Guidance**: Personalized job and course recommendations
- **Application Tracking**: Real-time status updates on job applications
- **User Authentication with OTP (Email)**: Secure login and verification using one-time passwords sent via SMTP

## 🛠️ Tech Stack

### Backend
- **.NET 9 (ASP.NET MVC)** – Modern web framework for building scalable applications
- **C# 12** – Primary backend programming language
- **Entity Framework Core 9** – ORM for data access and LINQ queries
- **Unit of Work & Repository Pattern** – Transaction-safe, modular data management
- **Custom User Authentication** – Built-in role-based user system
- **AutoMapper** – Object-to-object mapping between DTOs and entities
- **MailKit** – For email notifications and account verification
- **Cloudinary** – Cloud-based media storage for images and documents
- **CSVHelper** – Import/export candidate or job data in CSV format
- **OpenXML + iText7** – For generating and reading DOCX/PDF reports
- **Pgvector** – AI-ready vector similarity search within PostgreSQL
- **Caching (PostgreSQL Cache Provider)** – Optimized query caching for performance

### Frontend
- **Tailwind CSS** – Utility-first CSS framework for responsive design
- **JavaScript (ES6)** – For dynamic frontend interactivity
- **Razor Views** – Server-side rendered UI templates

### Database
- **PostgreSQL** – Primary relational database
- **Pgvector Extension** – For semantic search and AI integrations

### Architecture & Design
- **Layered Architecture (MVC + Service + Repository)** – Clean separation of concerns
- **Dependency Injection (DI)** – Built-in service management via .NET Core
- **Unit of Work Pattern** – Ensures atomic database transactions
- **Configuration Management** – `appsettings.json` & environment variables

### Development Tools
- **Visual Studio 2022** – IDE for .NET 9 development
- **NuGet** – Package manager for dependencies
- **Git & GitHub** – Version control and CI/CD
- **EF Core Migrations** – Database schema evolution

### AI & Extensions
- **Pgvector + EF Core Integration** – Enables vector embeddings for AI search
- **Cloudinary Integration** – Smart asset handling for resumes and documents
- **Scoring & Analysis Services (Pluggable)** – Extendable for AI-powered candidate ranking

### AI/ML Components
- **Resume Parsing**: Text extraction from PDF/images using OCR
- **Skill Matching**: Cosine similarity with TF-IDF vectorization
- **Job Recommendations**: ML-based career guidance system
- **Scoring Algorithms**: Multi-factor candidate evaluation

## 📁 Project Structure

```
Companion-AI/
│
├── Dotnet-MVC/                       # Frontend and main web application (ASP.NET MVC)
│   ├── Controllers/                  # Handles HTTP requests and responses
│   ├── Models/                       # Contains data models and entity classes
│   ├── Views/                        # Razor (.cshtml) views organized by controller
│   ├── wwwroot/                      # Static files (CSS, JS, images)
│   ├── Attributes/                   # Custom attributes (e.g., authorization filters, Email service)
│   ├── ViewModels/                   # View model classes combining data for views
│   ├── Migrations/                   # Entity Framework migration files
│   ├── Data/                         # Database context and initialization scripts
│   ├── appsettings.json              # Application configuration (e.g., connection strings)
│   └── Program.cs                    # Application entry point, Configures services, middleware, and routing
│
└── Python-Backend/                   # AI and API backend (FastAPI)
    ├── agent/                        # AI agent implementation files
    ├── utils/                        # Utility and helper functions
    ├── feedback/                     # User feedback handling and processing
    ├── app.py                        # FastAPI main application file
    ├── requirements.txt              # Python dependencies list
    └── Dockerfile                    # Docker image configuration

```

## 🚦 Getting Started

### Prerequisites
- Python 3.8+
- .NET SDK 7.0+ (or the version your project targets)
- Visual Studio 2022+ (or VS Code with C# extension)
- Optional: NuGet CLI for restoring packages
- PostgreSQL database

### DotNet-MVC Setup

```bash
cd website
# 🚀 Setting Up .NET MVC App

## Prerequisites

* .NET SDK 7.0+ (or the version your project targets)
* Visual Studio 2022+ (or VS Code with C# extension)
* PostgreSQL
* Optional: NuGet CLI for restoring packages

---

## Steps

### 1. Clone the repository

```bash
cd Dotnet-MVC
```

### 2. Open the project

* Open the `.sln` file in Visual Studio, or open the folder in VS Code.

### 3. Restore NuGet packages

* Visual Studio usually restores automatically on project load.
* Or via CLI:

```bash
dotnet restore
```

### 4. Configure database connection

* Update `appsettings.json` with your database credentials:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=CompanionAI;Username=postgres;Password=root"
}
```

### 5. Run database migrations (if using EF Core)

```bash
dotnet ef database update
```

This will create tables in your configured database.

### 6. Configure other settings

* Cloudinary, SMTP, or other API keys in `appsettings.json` as needed.

### 7. Build the project

```bash
dotnet build
```

### 8. Run the project

```bash
dotnet run
```

* By default, the app will run on `https://localhost:5001` or `http://localhost:5000`.
* Open the browser to verify.

---

## Optional: Using Visual Studio

1. Open the `.sln` file.
2. Set **Startup Project** to your MVC project.
3. Press **F5** to run with IIS Express.

---

## Notes

* Ensure your database server is running before starting the app.
* Configure `ConnectionStrings` properly based on your database.
* Make sure any required API keys or settings are added to `appsettings.json`.

```

### Backend Setup

```bash
cd python-backend
pip install -r requirements.txt
cp .env.example .env
python main.py
```

### Environment Variables

#### DotNet (Appsetting.json)
```Appsetting.json
SMTP_SERVER=smtp.gmail.com
SMTP_PORT=587
SMTP_SENDER_NAME=your_sender_name
SMTP_SENDER_EMAIL=your_sender_email
SMTP_USERNAME=your_email_username
SMTP_PASSWORD=your_email_password
```

#### Python Backend (.env)
```env
GROQ_API_KEY=your_groq_api_key
SERPER_API_KEY=your_serper_api_key

# Cloudinary (File/Media Storage)
CLOUDINARY_CLOUD_NAME=your_cloud_name
CLOUDINARY_API_KEY=your_cloudinary_api_key
CLOUDINARY_API_SECRET=your_cloudinary_api_secret
```

## 🔧 Core Components

### Resume Agent
Intelligent resume parsing and analysis system that extracts skills, experience, and qualifications to generate comprehensive candidate profiles.

### Scoring Agent
Multi-dimensional evaluation system that scores candidates based on technical skills, experience, certifications, projects, and soft skills.

### Scheduler Communication Agent
Automated communication system for interview scheduling and candidate engagement with personalized messaging.

### Job Insights Engine
Advanced analytics platform providing market trends, salary insights, and career growth predictions.

## 🎯 User Roles

### HR Professional
- Create and manage job postings
- Build custom aptitude tests
- Review and score applications
- Schedule interviews
- Generate hiring reports

### Candidate
- Browse job opportunities
- Take skill assessments
- Submit applications
- Track application status
- Access career insights

## 🔒 Security Features

- Multi-factor authentication via SMTP OTP based
- Role-based access control
- Secure file upload handling
- Data encryption and privacy protection (using sha256 with salting for password storing)
- CORS and security middleware

## 📊 Analytics & Insights

- Real-time job market analysis
- Salary benchmarking by role and location
- Skills demand forecasting
- Industry growth trends
- Personalized career recommendations

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 📞 Support

For support and questions:
- Email: deepcoding15@gmail.com

## 🏆 Acknowledgments

Built with modern web technologies and AI frameworks to create an innovative hiring solution that benefits both employers and job seekers.