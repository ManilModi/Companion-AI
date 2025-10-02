using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DotnetMVCApp.Attributes;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using DotnetMVCApp.ViewModels.Candidate;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DotnetMVCApp.Controllers
{
    [SessionAuthorize("Candidate")] // Only candidates can access
    public class CandidateController : Controller
    {
        private readonly IUserRepo _userRepo;
        private readonly IInterviewrepo _interviewrepo;
        private readonly Cloudinary _cloudinary;
        private readonly HttpClient _httpClient;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CandidateController(
            IUserRepo userRepo,
            IInterviewrepo interviewrepo,
            IUnitOfWork unitOfWork,
            Cloudinary cloudinary,
            IHttpClientFactory httpClientFactory,
            IMapper mapper)
        {
            _userRepo = userRepo;
            _interviewrepo = interviewrepo;
            _unitOfWork = unitOfWork;
            _cloudinary = cloudinary;
            _httpClient = httpClientFactory.CreateClient();
            _mapper = mapper;
        }

        // ----------------- Helper -----------------
        private int GetCurrentUserId()
        {
            // 1️⃣ Try cookie authentication
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (idClaim != null && int.TryParse(idClaim.Value, out int userId))
                    return userId;
            }

            // 2️⃣ Fallback to session
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        // Candidate Dashboard
        public IActionResult Dashboard()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account"); // fallback

            var user = _userRepo.GetUserById(userId);
            if (user == null) return NotFound();

            var extracted = !string.IsNullOrEmpty(user.ExtractedInfo)
                ? JObject.Parse(user.ExtractedInfo)
                : new JObject();

            var vm = new DashboardViewModel
            {
                UserId = user.UserId,
                Name = extracted["name"]?.ToString() ?? user.Username,
                Email = extracted["email"]?.ToString() ?? user.Email,
                ContactNo = extracted["contact_no"]?.ToString(),
                ResumeUrl = user.ResumeUrl,
                Skills = extracted["skills"] != null
                    ? extracted["skills"].ToObject<List<string>>()
                    : new List<string>(),
                ExperienceSummary = extracted["experience"]?.ToString(),
                ProjectsBuilt = extracted["projects_built"] != null
                    ? extracted["projects_built"].ToObject<List<string>>()
                    : new List<string>(),
                Achievements = extracted["achievements_like_awards_and_certifications"] != null
                    ? extracted["achievements_like_awards_and_certifications"].ToObject<List<string>>()
                    : new List<string>()
            };

            return View("~/Views/User/Candidate/Dashboard.cshtml", vm);
        }

        [HttpPost]
        public async Task<IActionResult> UploadResume(IFormFile resumeFile, int userId)
        {
            if (resumeFile == null || resumeFile.Length == 0)
            {
                ViewData["Error"] = "Please select a valid resume file.";
                return RedirectToAction("Dashboard", new { id = userId });
            }

            string resumeUrl;

            using (var stream = resumeFile.OpenReadStream())
            {
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(resumeFile.FileName, stream),
                    PublicId = $"resumes/{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(resumeFile.FileName)}",
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    ViewData["Error"] = "Resume upload failed. Try again.";
                    return RedirectToAction("Dashboard", new { id = userId });
                }

                resumeUrl = uploadResult.SecureUrl.ToString();
            }

            string responseString;
            using (var form = new MultipartFormDataContent())
            using (var stream = resumeFile.OpenReadStream())
            {
                var streamContent = new StreamContent(stream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                form.Add(streamContent, "file", resumeFile.FileName);

                var response = await _httpClient.PostAsync("http://localhost:8000/parse-resume", form);
                if (!response.IsSuccessStatusCode)
                {
                    ViewData["Error"] = "Resume uploaded but parsing failed.";
                    return RedirectToAction("Dashboard", new { id = userId });
                }

                responseString = await response.Content.ReadAsStringAsync();
            }

            var user = _userRepo.GetUserById(userId);
            if (user != null)
            {
                user.ResumeUrl = resumeUrl;
                user.ExtractedInfo = responseString;
                _userRepo.Update(user);
                _unitOfWork.Save();
            }

            ViewData["Success"] = "Resume uploaded and parsed successfully!";
            return RedirectToAction("Dashboard", new { id = userId });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadResume(int userId)
        {
            var user = _userRepo.GetUserById(userId);
            if (user == null || string.IsNullOrEmpty(user.ResumeUrl))
            {
                return NotFound("Resume not found.");
            }

            var response = await _httpClient.GetAsync(user.ResumeUrl);
            if (!response.IsSuccessStatusCode)
            {
                return NotFound("Unable to download resume from storage.");
            }

            var fileBytes = await response.Content.ReadAsByteArrayAsync();
            var fileName = Path.GetFileName(user.ResumeUrl);
            if (!fileName.Contains('.'))
            {
                fileName += ".pdf";
            }

            return File(fileBytes, "application/octet-stream", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> JobSearch(string query = "", string sortBy = "similarity", string status = "")
        {
            int userId = GetCurrentUserId();
            var allJobs = _unitOfWork.Jobs.GetAllJobs();

            // 1️⃣ Filter by search query
            if (!string.IsNullOrWhiteSpace(query))
            {
                allJobs = allJobs.Where(j =>
                    (j.JobTitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (j.Company?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            // 2️⃣ Filter by status (active / closed)
            if (!string.IsNullOrWhiteSpace(status))
            {
                var now = DateTime.Now;
                allJobs = status.Equals("active", StringComparison.OrdinalIgnoreCase)
                    ? allJobs.Where(j => j.CloseTime > now).ToList()
                    : allJobs.Where(j => j.CloseTime <= now).ToList();
            }

            // Applied jobs for the current user
            var appliedJobIds = _unitOfWork.UserJobs.GetJobsByUser(userId)
                                                   .Select(uj => uj.JobId)
                                                   .ToHashSet();

            // Map jobs to view models
            var model = _mapper.Map<List<JobSearchViewModel>>(allJobs);

            // Current candidate details
            var currentUser = _unitOfWork.Users.GetUserById(userId);
            var extractedInfo = currentUser?.ExtractedInfo ?? "{}";

            // 3️⃣ Get candidate embedding (once)
            float[] candidateEmbedding = null;
            try
            {
                var resumeResponse = await _httpClient.PostAsJsonAsync(
                    "http://localhost:8000/embed",
                    new { text = extractedInfo }
                );
                if (resumeResponse.IsSuccessStatusCode)
                {
                    var json = await resumeResponse.Content.ReadFromJsonAsync<EmbedResponse>();
                    candidateEmbedding = json?.Embedding;
                }
            }
            catch
            {
                // log exception if needed
            }

            // 4️⃣ Process each job
            foreach (var jobVm in model)
            {
                var jobEntity = allJobs.First(j => j.JobId == jobVm.JobId);

                // Applied status
                jobVm.HasApplied = appliedJobIds.Contains(jobVm.JobId);

                // Active or closed
                jobVm.Status = jobEntity.CloseTime > DateTime.Now ? "active" : "closed";

                // Applicants count
                jobVm.ApplicantsCount = jobEntity.Applicants?.Count ?? 0;

                // Candidate resume (json string for debugging/feedback)
                jobVm.CandidateResumeJson = extractedInfo;

                // Job description (fetched from URL if provided)
                string jobText = "No description provided.";
                if (!string.IsNullOrEmpty(jobEntity.JobDescription))
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(jobEntity.JobDescription);
                        if (response.IsSuccessStatusCode)
                        {
                            jobText = await response.Content.ReadAsStringAsync();
                        }
                    }
                    catch
                    {
                        // log exception if needed
                    }
                }
                jobVm.Description = jobText;

                // Feedback status for current user
                var feedback = _unitOfWork.Feedbacks.GetByUserAndJob(userId, jobVm.JobId);
                jobVm.HasFeedback = feedback != null;
                jobVm.FeedbackId = feedback?.FeedbackId;

                // ⭐ New: calculate feedback stats
                var allFeedbacks = _unitOfWork.Feedbacks.GetByJob(jobVm.JobId).ToList();
                jobVm.FeedbackCount = allFeedbacks.Count;

                if (jobVm.FeedbackCount >= 5)
                {
                    // Map -1 → 1, 0 → 3, 1 → 5
                    var mappedScores = allFeedbacks.Select(f =>
                        f.Sentiment == 1 ? 5 :
                        f.Sentiment == 0 ? 3 :
                        f.Sentiment == -1 ? 1 : 0
                    );

                    jobVm.AverageSentiment = mappedScores.Average();
                }


                // Similarity (resume vs job embedding)
                if (candidateEmbedding != null && jobEntity.Embedding != null)
                {
                    jobVm.Similarity = CosineSimilarity(candidateEmbedding, jobEntity.Embedding);
                }
                else
                {
                    jobVm.Similarity = 0f;
                }
            }

            // 5️⃣ Sort results
            model = sortBy switch
            {
                "recent" => model.OrderByDescending(j => j.OpenTime).ToList(),
                "applicants" => model.OrderByDescending(j => j.ApplicantsCount).ToList(),
                _ => model.OrderByDescending(j => j.Similarity).ToList() // default: similarity
            };

            return View("~/Views/User/Candidate/JobSearch.cshtml", model);
        }



        // Embed response class
        public class EmbedResponse
        {
            public float[] Embedding { get; set; }
        }

        private float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0f, normA = 0f, normB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-10);
        }

        private float[] Normalize(float[] vec)
        {
            float norm = (float)Math.Sqrt(vec.Sum(x => x * x));
            return vec.Select(x => x / (norm + 1e-10f)).ToArray();
        }



        [HttpGet]
        public IActionResult ApplyJob(int id)
        {
            int userId = GetCurrentUserId();

            bool alreadyApplied = _unitOfWork.UserJobs.Exists(userId, id);
            if (!alreadyApplied)
            {
                _unitOfWork.UserJobs.ApplyToJob(userId, id);
                _unitOfWork.Save();
                ViewData["Message"] = "Applied successfully!";
            }
            else
            {
                ViewData["Message"] = "You have already applied for this job.";
            }

            return RedirectToAction("JobSearch");
        }

        [HttpPost]
        public IActionResult WithdrawJob(int id)
        {
            int userId = GetCurrentUserId();

            var userJob = _unitOfWork.UserJobs.GetByUserAndJob(userId, id);
            if (userJob != null)
            {
                _unitOfWork.UserJobs.Delete(userJob);
                _unitOfWork.Save();
            }

            return RedirectToAction("JobSearch");
        }

        [HttpGet]
        public async Task<IActionResult> SmartJobSearch()
        {
            int userId = GetCurrentUserId();
            var user = _unitOfWork.Users.GetUserById(userId);
            if (user == null || string.IsNullOrEmpty(user.ExtractedInfo))
            {
                ViewData["Error"] = "No resume info found. Please upload your resume first.";
                return RedirectToAction("Dashboard");
            }

            // 1️⃣ Get candidate text from resume JSON (example: skills + experience summary)
            var extracted = JObject.Parse(user.ExtractedInfo);
            var candidateText = string.Join(" ",
                extracted["skills"]?.ToObject<List<string>>() ?? new List<string>(),
                extracted["experience"]?.ToString() ?? ""
            );

            if (string.IsNullOrEmpty(candidateText))
            {
                ViewData["Error"] = "Cannot extract meaningful info from resume.";
                return RedirectToAction("Dashboard");
            }

            // 2️⃣ Call FastAPI /embed to get embedding
            var response = await _httpClient.PostAsJsonAsync("http://127.0.0.1:8000/embed", new { text = candidateText });
            if (!response.IsSuccessStatusCode)
            {
                ViewData["Error"] = "Embedding service failed.";
                return RedirectToAction("Dashboard");
            }

            var json = await response.Content.ReadFromJsonAsync<JobRepo.EmbedResponse>();
            if (json?.Embedding == null || json.Embedding.Length == 0)
            {
                ViewData["Error"] = "Embedding returned empty vector.";
                return RedirectToAction("Dashboard");
            }

            // 3️⃣ Get top matching jobs by cosine similarity
            var topJobs = await _unitOfWork.Jobs.GetTopJobsByCandidateEmbeddingAsync(json.Embedding);

            // 4️⃣ Map to view model
            var model = _mapper.Map<List<JobSearchViewModel>>(topJobs);

            return View("~/Views/User/Candidate/SmartJobSearch.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> SearchJobs([FromForm] string query = "")
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var user = _unitOfWork.Users.GetUserById(userId);
            if (user == null || string.IsNullOrEmpty(user.ExtractedInfo))
            {
                ViewData["Error"] = "Please upload your resume first.";
                return RedirectToAction("Dashboard");
            }

            string extractedInfo = user.ExtractedInfo;

            // ✅ Candidate embedding (not needed for search, but you may keep it for personalization)
            float[] candidateEmbedding = null;
            try
            {
                var embedResponse = await _httpClient.PostAsJsonAsync(
                    "http://localhost:8000/embed",
                    new { text = extractedInfo }
                );
                if (embedResponse.IsSuccessStatusCode)
                {
                    var json = await embedResponse.Content.ReadFromJsonAsync<EmbedResponse>();
                    candidateEmbedding = json?.Embedding;
                }
            }
            catch { }

            // ✅ Query embedding
            float[] queryEmbedding = null;
            if (!string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    var response = await _httpClient.PostAsJsonAsync(
                        "http://localhost:8000/embed",
                        new { text = query }
                    );
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadFromJsonAsync<EmbedResponse>();
                        queryEmbedding = json?.Embedding;
                    }
                }
                catch { }
            }

            // ✅ Fetch all jobs
            var allJobs = _unitOfWork.Jobs.GetAllJobs();
            var appliedJobIds = _unitOfWork.UserJobs.GetJobsByUser(userId)
                                                   .Select(uj => uj.JobId)
                                                   .ToHashSet();
            var model = _mapper.Map<List<JobSearchViewModel>>(allJobs);

            bool anyMatch = false;

            foreach (var jobVm in model)
            {
                var jobEntity = allJobs.First(j => j.JobId == jobVm.JobId);

                float similarity = 0f;

                // ✅ Use pre-stored job embeddings from DB
                if (queryEmbedding != null && jobEntity.Embedding != null)
                {
                    similarity = CosineSimilarity(Normalize(queryEmbedding), Normalize(jobEntity.Embedding));
                }

                if (similarity > 0.01f) anyMatch = true;

                jobVm.Similarity = similarity;
                jobVm.HasApplied = appliedJobIds.Contains(jobVm.JobId);
                jobVm.Status = jobEntity.CloseTime > DateTime.Now ? "active" : "closed";
                jobVm.ApplicantsCount = jobEntity.Applicants?.Count ?? 0;
                jobVm.Description = jobEntity.JobDescription ?? "No description provided.";
                jobVm.CandidateResumeJson = extractedInfo;
            }

            if (!anyMatch)
            {
                foreach (var jobVm in model)
                {
                    jobVm.Similarity = 0f;
                }
            }

            // ✅ Sort by similarity (descending)
            model = model.OrderByDescending(j => j.Similarity).ToList();

            return View("~/Views/User/Candidate/JobSearch.cshtml", model);
        }




        public IActionResult JobMarket()
        {
            ViewData["ActivePage"] = "JobMarket";
            return View("~/Views/User/Candidate/JobMarket.cshtml");
        }

        [HttpGet]
        public IActionResult ThirdPartyJobs()
        {
            return View("~/Views/User/Candidate/ThirdPartyjobs.cshtml",
                new JobSearchAIResponse { Jobs = new List<JobSearchAIViewModel>() });
        }

        [HttpPost]
        public async Task<IActionResult> ThirdPartyJobs(string customPrompt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customPrompt))
                {
                    return View("~/Views/User/Candidate/ThirdPartyjobs.cshtml",
                        new JobSearchAIResponse { Jobs = new List<JobSearchAIViewModel>() });
                }

                // Prepare request body for FastAPI
                var requestObj = new { custom_prompt = customPrompt };
                string requestJson = JsonSerializer.Serialize(requestObj);

                using var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                // ✅ Call FastAPI POST /search_jobs
                var response = await _httpClient.PostAsync("http://127.0.0.1:8000/search_jobs", httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    return View("~/Views/User/Candidate/ThirdPartyjobs.cshtml",
                        new JobSearchAIResponse { Jobs = new List<JobSearchAIViewModel>() });
                }

                var json = await response.Content.ReadAsStringAsync();

                var jobs = JsonSerializer.Deserialize<JobSearchAIResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return View("~/Views/User/Candidate/ThirdPartyjobs.cshtml", jobs);
            }
            catch (Exception)
            {
                return View("~/Views/User/Candidate/ThirdPartyjobs.cshtml",
                    new JobSearchAIResponse { Jobs = new List<JobSearchAIViewModel>() });
            }
        }

        [HttpGet]
        public async Task<IActionResult> MockInterview(int id)
        {
            try
            {
                int userId = GetCurrentUserId();

                var user = _unitOfWork.Users.GetUserById(userId);
                if (user == null)
                {
                    ViewData["Error"] = "User not found.";
                    return RedirectToAction("Dashboard");
                }

                var job = _unitOfWork.Jobs.GetJobById(id);
                if (job == null || string.IsNullOrWhiteSpace(job.JobDescription))
                {
                    ViewData["Error"] = "No job description found for this job.";
                    return RedirectToAction("Dashboard");
                }

                var hasApplied = _unitOfWork.UserJobs.HasApplied(userId, id);
                if (!hasApplied)
                {
                    ViewData["Error"] = "You must apply for this job before attempting a mock interview.";
                    return RedirectToAction("JobSearch");
                }

                string jobDescription;
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(job.JobDescription);
                    if (!response.IsSuccessStatusCode)
                    {
                        ViewData["Error"] = "Failed to load job description.";
                        return RedirectToAction("Dashboard");
                    }

                    jobDescription = await response.Content.ReadAsStringAsync();
                }

                var viewModel = new MockInterviewViewModel
                {
                    JobId = id,
                    JobDescription = jobDescription
                    // 🚫 No ScoreJson here – always start fresh
                };

                return View("~/Views/User/Candidate/MockInterview.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                ViewData["Error"] = "Error loading mock interview: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }



        [HttpPost]
        public async Task<IActionResult> SaveInterviewResult([FromBody] MockInterviewViewModel model)
        {
            try
            {
                int userId = GetCurrentUserId();

                var interview = new Interview
                {
                    UserId = userId,
                    JobId = model.JobId,
                    Score = model.ScoreJson,
                    CreatedAt = DateTime.UtcNow
                };

                _interviewrepo.Add(interview);

                return Ok(new { message = "Interview saved", interviewId = interview.InterviewId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult MockInterviewHistory()
        {
            int userId = GetCurrentUserId();
            var interviews = _interviewrepo.GetByUser(userId);

            var model = interviews.Select(i => new MockInterviewHistoryViewModel
            {
                InterviewId = i.InterviewId,
                JobId = i.JobId,
                JobTitle = i.Job?.JobTitle ?? "Unknown Job",
                Score = i.Score,
                CreatedAt = i.CreatedAt
            }).ToList();

            return View("~/Views/User/Candidate/MockInterviewHistory.cshtml", model);
        }



    }
}
