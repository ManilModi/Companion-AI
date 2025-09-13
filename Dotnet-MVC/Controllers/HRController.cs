using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using DotnetMVCApp.ViewModels.HR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace DotnetMVCApp.Controllers
{
    public class HRController : Controller
    {
        private readonly IJobRepo _jobRepo;
        private readonly IUserJobRepo _userJobRepo;
        private readonly IUserRepo _userRepo;
        private readonly IInterviewrepo _interviewRepo;
        private readonly Cloudinary _cloudinary;
        private readonly IUnitOfWork _unitOfWork;

        public HRController(IJobRepo jobRepo, IUserJobRepo userJobRepo, IUserRepo userRepo, IInterviewrepo interviewRepo, Cloudinary cloudinary, IUnitOfWork unitOfWork)
        {
            _jobRepo = jobRepo;
            _userJobRepo = userJobRepo;
            _userRepo = userRepo;
            _interviewRepo = interviewRepo;
            _cloudinary = cloudinary;
            _unitOfWork = unitOfWork;
        }

        private int GetCurrentHrId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int hrId))
                return hrId;

            return 0;
        }

        private async Task<string> GetJobDescriptionTextAsync(string jdUrl)
        {
            if (string.IsNullOrEmpty(jdUrl))
                return "[No job description]";

            try
            {
                using var httpClient = new HttpClient();
                var fileBytes = await httpClient.GetByteArrayAsync(jdUrl);

                if (jdUrl.EndsWith(".txt"))
                {
                    return Encoding.UTF8.GetString(fileBytes);
                }
                else if (jdUrl.EndsWith(".docx"))
                {
                    using var ms = new MemoryStream(fileBytes);
                    using var doc = WordprocessingDocument.Open(ms, false);
                    return string.Join(" ",
                        doc.MainDocumentPart.Document.Body
                           .Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                           .Select(p => p.InnerText));

                }
                else if (jdUrl.EndsWith(".pdf"))
                {
                    using var ms = new MemoryStream(fileBytes);
                    using var pdfReader = new PdfReader(ms);
                    using var pdfDoc = new PdfDocument(pdfReader);
                    var sb = new StringBuilder();

                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var strategy = new SimpleTextExtractionStrategy();
                        string text = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
                        sb.AppendLine(text);
                    }

                    return sb.ToString();
                }

                return "[Unsupported file format]";
            }
            catch (Exception ex)
            {
                return $"[Error fetching JD: {ex.Message}]";
            }
        }

        public IActionResult Overview()
        {
            int hrId = GetCurrentHrId();

            var jobs = _jobRepo.GetAllJobs().Where(j => j.PostedByUserId == hrId).ToList();
            var applicants = jobs.SelectMany(j => j.Applicants).Count();
            var interviews = _interviewRepo.GetAllInterview()
                                           .Where(i => i.Job.PostedByUserId == hrId)
                                           .Count();

            var model = new OverviewViewModel
            {
                ActiveJobs = jobs.Count(j => j.CloseTime > DateTime.Now),
                TotalApplicants = applicants,
                UpcomingInterviews = interviews
            };

            return View("~/Views/User/HR/Overview.cshtml", model);
        }

        private string UploadJobDescriptionToCloudinary(string jobDescription, string fileName)
        {
            // Convert job description text into a memory stream
            var bytes = Encoding.UTF8.GetBytes(jobDescription);
            using var stream = new MemoryStream(bytes);

            var uploadParams = new RawUploadParams()
            {
                File = new FileDescription(fileName + ".txt", stream),
                Folder = "JDs"
            };

            var uploadResult = _cloudinary.Upload(uploadParams);

            return uploadResult.SecureUrl?.ToString();
        }


        [HttpGet]
        public IActionResult CreateJob()
        {
            return View("~/Views/User/HR/CreateJob.cshtml");
        }

        [HttpPost]
        public IActionResult CreateJob(CreateJobViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/User/HR/CreateJob.cshtml", model);

            string jdUrl = UploadJobDescriptionToCloudinary(model.JobDescription, model.JobTitle.Replace(" ", "_"));

            var job = new Job
            {
                JobTitle = model.JobTitle,
                JobDescription = jdUrl,
                TechStacks = model.TechStacks,
                SkillsRequired = JsonSerializer.Serialize(
                    model.SkillsRequired?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ),
                OpenTime = DateTime.SpecifyKind(model.OpenTime, DateTimeKind.Utc),
                CloseTime = DateTime.SpecifyKind(model.CloseTime, DateTimeKind.Utc),
                Company = model.Company,
                Location = model.Location,
                JobType = model.JobType,
                SalaryRange = model.SalaryRange,
                PostedByUserId = GetCurrentHrId()
            };

            _jobRepo.Add(job);
            return RedirectToAction("JobListings");
        }

        [HttpGet]
        public async Task<IActionResult> JobListings()
        {
            int hrId = GetCurrentHrId();

            var jobs = _jobRepo.GetAllJobs()
                               .Where(j => j.PostedByUserId == hrId)
                               .ToList();

            var model = new List<JobListingViewModel>();

            foreach (var j in jobs)
            {
                string jdText = await GetJobDescriptionTextAsync(j.JobDescription);

                model.Add(new JobListingViewModel
                {
                    JobId = j.JobId,
                    JobTitle = j.JobTitle,
                    JobDescription = jdText,
                    TechStacks = j.TechStacks,
                    OpenTime = j.OpenTime,
                    CloseTime = j.CloseTime,
                    Status = j.CloseTime > DateTime.Now ? "Active" : "Closed",
                    Company = j.Company,
                    SalaryRange = j.SalaryRange,
                    Location = j.Location,
                    JobType = j.JobType
                });
            }

            return View("~/Views/User/HR/JobListings.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> Applicants(int jobId)
        {
            var job = _unitOfWork.Jobs.GetJobWithApplicantsAndUsers(jobId);
            if (job == null || job.PostedByUserId != GetCurrentHrId())
                return Unauthorized();

            // ✅ Fetch job description text (from Cloudinary URL)
            string jobDescription = "";
            if (!string.IsNullOrEmpty(job.JobDescription))
            {
                try
                {
                    using var client = new HttpClient();
                    var response = await client.GetAsync(job.JobDescription);
                    if (response.IsSuccessStatusCode)
                        jobDescription = await response.Content.ReadAsStringAsync();
                }
                catch
                {
                    jobDescription = "";
                }
            }

            var applicants = new List<ApplicantViewModel>();

            foreach (var a in job.Applicants)
            {
                ExtractedInfoModel extracted = new ExtractedInfoModel();

                if (!string.IsNullOrEmpty(a.User?.ExtractedInfo))
                {
                    try
                    {
                        extracted = JsonSerializer.Deserialize<ExtractedInfoModel>(
                            a.User.ExtractedInfo,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        ) ?? new ExtractedInfoModel();
                    }
                    catch { extracted = new ExtractedInfoModel(); }
                }

                // ✅ Call FastAPI only if score not already saved
                Dictionary<string, int>? scores = null;
                if (string.IsNullOrEmpty(a.Score) && !string.IsNullOrEmpty(a.User?.ExtractedInfo))
                {
                    try
                    {
                        using var client = new HttpClient();

                        var requestBody = new
                        {
                            resume_json = JsonSerializer.Deserialize<object>(a.User.ExtractedInfo),
                            job_description = jobDescription
                        };

                        var response = await client.PostAsJsonAsync("http://localhost:8000/score-resume/", requestBody);
                        if (response.IsSuccessStatusCode)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();

                            // FastAPI returns scores in JSON → store in DB
                            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseString);
                            if (result != null && result.ContainsKey("scores"))
                            {
                                scores = JsonSerializer.Deserialize<Dictionary<string, int>>(result["scores"].ToString() ?? "{}");

                                // Save into UserJob.Score
                                a.Score = JsonSerializer.Serialize(scores);
                                _unitOfWork.Save();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Scoring failed for applicant {a.UserId}: {ex.Message}");
                        scores = new Dictionary<string, int>();
                    }
                }
                else
                {
                    scores = !string.IsNullOrEmpty(a.Score)
                        ? JsonSerializer.Deserialize<Dictionary<string, int>>(a.Score)
                        : new Dictionary<string, int>();
                }

                applicants.Add(new ApplicantViewModel
                {
                    UserId = a.UserId,
                    Name = extracted?.Name ?? a.User?.Username ?? "",
                    Email = extracted?.Email ?? a.User?.Email ?? "",
                    ContactNo = extracted?.ContactNo ?? "",
                    ResumeUrl = a.User?.ResumeUrl ?? "",
                    Skills = extracted?.Skills ?? new List<string>(),
                    ExperienceSummary = extracted?.ExperienceSummary ?? "",
                    TotalExperienceYears = extracted?.TotalExperienceYears != null
                        ? (int?)Math.Round(extracted.TotalExperienceYears.Value)
                        : null,
                    ProjectsBuilt = extracted?.ProjectsBuilt ?? new List<string>(),
                    Achievements = extracted?.Achievements ?? new List<string>(),
                    Scores = scores
                });
            }

            // ✅ Sort by TotalScore before sending to view
            return View("~/Views/User/HR/Applicants.cshtml",
                applicants.OrderByDescending(a => a.Scores != null && a.Scores.ContainsKey("TotalScore")
                                                ? a.Scores["TotalScore"] : 0)
                          .ToList());
        }




        [HttpGet]
        public async Task<IActionResult> EditJob(int jobId)
        {
            var job = _jobRepo.GetJobById(jobId);
            if (job == null) return NotFound();

            string jdText = await GetJobDescriptionTextAsync(job.JobDescription);

            var model = new CreateJobViewModel
            {
                JobId = job.JobId,
                JobTitle = job.JobTitle,
                JobDescription = jdText,
                TechStacks = job.TechStacks,
                SkillsRequired = string.Join(", ",
                    JsonSerializer.Deserialize<List<string>>(job.SkillsRequired ?? "[]") ?? new List<string>()),
                OpenTime = job.OpenTime.ToUniversalTime(),
                CloseTime = job.CloseTime.ToUniversalTime(),
                Company = job.Company,
                Location = job.Location,
                JobType = job.JobType,
                SalaryRange = job.SalaryRange
            };

            return View("~/Views/User/HR/EditJob.cshtml", model);
        }

        [HttpPost]
        public IActionResult EditJob(int jobId, CreateJobViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/User/HR/EditJob.cshtml", model);

            var job = _jobRepo.GetJobById(jobId);
            if (job == null) return NotFound();

            string jdUrl = UploadJobDescriptionToCloudinary(model.JobDescription, model.JobTitle.Replace(" ", "_"));

            job.JobTitle = model.JobTitle;
            job.JobDescription = jdUrl;
            job.TechStacks = model.TechStacks;
            job.SkillsRequired = JsonSerializer.Serialize(
                model.SkillsRequired?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            );
            job.OpenTime = DateTime.SpecifyKind(model.OpenTime, DateTimeKind.Utc);
            job.CloseTime = DateTime.SpecifyKind(model.CloseTime, DateTimeKind.Utc);
            job.Company = model.Company;
            job.Location = model.Location;
            job.JobType = model.JobType;
            job.SalaryRange = model.SalaryRange;

            _jobRepo.Update(job);

            return RedirectToAction("JobListings");
        }

        [HttpPost]
        public IActionResult DeleteJob(int jobId)
        {
            var job = _jobRepo.GetJobById(jobId);

            if (job == null /* || job.PostedByUserId != GetCurrentHrId() */)
                return NotFound();

            _jobRepo.Delete(jobId);

            return RedirectToAction("JobListings");
        }
    }
}
