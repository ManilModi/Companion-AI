using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DotnetMVCApp.Attributes;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using DotnetMVCApp.ViewModels.Candidate;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace DotnetMVCApp.Controllers
{
    [SessionAuthorize("Candidate")]
    public class CandidateController : Controller
    {
        private readonly IUserRepo _userRepo;
        private readonly Cloudinary _cloudinary;
        private readonly HttpClient _httpClient;
        private readonly IJobRepo _jobRepo;

        public CandidateController(IUserRepo userRepo, IJobRepo jobRepo, Cloudinary cloudinary, IHttpClientFactory httpClientFactory)
        {
            _userRepo = userRepo;
            _cloudinary = cloudinary;
            _httpClient = httpClientFactory.CreateClient();
            _jobRepo = jobRepo;
        }

        // --- Helper: Get current candidate userId from cookie or session ---
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
        public async Task<IActionResult> UploadResume(IFormFile resumeFile)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (resumeFile == null || resumeFile.Length == 0)
            {
                TempData["Error"] = "Please select a valid resume file.";
                return RedirectToAction("Dashboard");
            }

            string resumeUrl;

            // Upload resume to Cloudinary
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
                    TempData["Error"] = "Resume upload failed. Try again.";
                    return RedirectToAction("Dashboard");
                }

                resumeUrl = uploadResult.SecureUrl.ToString();
            }

            // Send to FastAPI parser
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
                    TempData["Error"] = "Resume uploaded but parsing failed.";
                    return RedirectToAction("Dashboard");
                }

                responseString = await response.Content.ReadAsStringAsync();
            }

            // Save in DB
            var user = _userRepo.GetUserById(userId);
            if (user != null)
            {
                user.ResumeUrl = resumeUrl;
                user.ExtractedInfo = responseString;
                _userRepo.Update(user);
            }

            TempData["Success"] = "Resume uploaded and parsed successfully!";
            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> DownloadResume()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var user = _userRepo.GetUserById(userId);
            if (user == null || string.IsNullOrEmpty(user.ResumeUrl)) return NotFound("Resume not found.");

            var response = await _httpClient.GetAsync(user.ResumeUrl);
            if (!response.IsSuccessStatusCode) return NotFound("Unable to download resume from storage.");

            var fileBytes = await response.Content.ReadAsByteArrayAsync();
            var fileName = Path.GetFileName(user.ResumeUrl);
            if (!fileName.Contains('.')) fileName += ".pdf";

            return File(fileBytes, "application/octet-stream", fileName);
        }

        [HttpGet]
        public IActionResult JobSearch()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var jobs = _jobRepo.GetAllJobs();

            var model = jobs.Select(j => new JobSearchViewModel
            {
                JobId = j.JobId,
                JobTitle = j.JobTitle,
                Company = j.Company ?? "Default Company",
                Location = j.Location ?? "Not specified",
                JobType = j.JobType ?? "Full time",
                SalaryRange = j.SalaryRange ?? "$ Not specified",
                PostedDate = j.OpenTime,
                Description = j.JobDescription ?? "",
                Status = j.CloseTime > DateTime.Now ? "active" : "closed",
                ApplicantsCount = j.Applicants?.Count ?? 0
            }).ToList();

            return View("~/Views/User/Candidate/JobSearch.cshtml", model);
        }
    }
}
