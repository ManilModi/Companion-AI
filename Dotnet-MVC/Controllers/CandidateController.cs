using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using DotnetMVCApp.ViewModels.Candidate;
using DotnetMVCApp.Attributes;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace DotnetMVCApp.Controllers
{
    [SessionAuthorize("Candidate")] // Only candidates can access
    public class CandidateController : Controller
    {
        private readonly IUserRepo _userRepo;
        private readonly Cloudinary _cloudinary;
        private readonly HttpClient _httpClient;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CandidateController(
            IUserRepo userRepo,
            IUnitOfWork unitOfWork,
            Cloudinary cloudinary,
            IHttpClientFactory httpClientFactory,
            IMapper mapper)
        {
            _userRepo = userRepo;
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
                TempData["Error"] = "Please select a valid resume file.";
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
                    TempData["Error"] = "Resume upload failed. Try again.";
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
                    TempData["Error"] = "Resume uploaded but parsing failed.";
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

            TempData["Success"] = "Resume uploaded and parsed successfully!";
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
        public async Task<IActionResult> JobSearch()
        {
            var jobs = _unitOfWork.Jobs.GetAllJobs();
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var appliedJobIds = _unitOfWork.UserJobs.GetJobsByUser(userId)
                                                   .Select(uj => uj.JobId)
                                                   .ToHashSet();

            var model = _mapper.Map<List<JobSearchViewModel>>(jobs);

            using var httpClient = new HttpClient();

            foreach (var jobVm in model)
            {
                jobVm.HasApplied = appliedJobIds.Contains(jobVm.JobId);
                jobVm.Status = jobVm.CloseTime > DateTime.Now ? "active" : "closed";
                jobVm.ApplicantsCount = jobs.First(j => j.JobId == jobVm.JobId).Applicants?.Count ?? 0;

                var jobEntity = jobs.First(j => j.JobId == jobVm.JobId);

                if (!string.IsNullOrEmpty(jobEntity.JobDescription))
                {
                    try
                    {
                        var response = await httpClient.GetAsync(jobEntity.JobDescription);
                        if (response.IsSuccessStatusCode)
                        {
                            jobVm.Description = await response.Content.ReadAsStringAsync();
                        }
                        else
                        {
                            jobVm.Description = "Unable to load description.";
                        }
                    }
                    catch
                    {
                        jobVm.Description = "Error fetching description.";
                    }
                }
                else
                {
                    jobVm.Description = "No description provided.";
                }
            }

            return View("~/Views/User/Candidate/JobSearch.cshtml", model);
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
                TempData["Message"] = "Applied successfully!";
            }
            else
            {
                TempData["Message"] = "You have already applied for this job.";
            }

            return RedirectToAction("JobSearch");
        }


        [HttpGet]
        public IActionResult ApplyJob(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
            {
                TempData["Message"] = "You must be logged in to apply for a job.";
                return RedirectToAction("Login", "Auth");
            }

            int userId = int.Parse(userIdClaim.Value);

            bool alreadyApplied = _unitOfWork.UserJobs.Exists(userId, id);
            if (!alreadyApplied)
            {
                _unitOfWork.UserJobs.ApplyToJob(userId, id);
                _unitOfWork.Save();
                TempData["Message"] = "Applied successfully!";
            }
            else
            {
                TempData["Message"] = "You have already applied for this job.";
            }

            return RedirectToAction("JobSearch");
        }

        [HttpPost]
        public IActionResult WithdrawJob(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var userJob = _unitOfWork.UserJobs.GetByUserAndJob(userId, id);
            if (userJob != null)
            {
                _unitOfWork.UserJobs.Delete(userJob);
                _unitOfWork.Save();
            }

            return RedirectToAction("JobSearch");
        }
    }
}
