using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DotnetMVCApp.Attributes; // <-- for SessionAuthorize
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
    [SessionAuthorize("HR")] // Only HR role can access
    public class HRController : Controller
    {
        private readonly IJobRepo _jobRepo;
        private readonly IUserJobRepo _userJobRepo;
        private readonly IUserRepo _userRepo;
        private readonly IInterviewrepo _interviewRepo;
        private readonly Cloudinary _cloudinary;

        public HRController(IJobRepo jobRepo, IUserJobRepo userJobRepo, IUserRepo userRepo, IInterviewrepo interviewRepo, Cloudinary cloudinary)
        {
            _jobRepo = jobRepo;
            _userJobRepo = userJobRepo;
            _userRepo = userRepo;
            _interviewRepo = interviewRepo;
            _cloudinary = cloudinary;
        }

        private int GetCurrentHrId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
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

        public IActionResult Applicants(int jobId)
        {
            var job = _jobRepo.GetJobById(jobId);
            if (job == null || job.PostedByUserId != GetCurrentHrId())
                return Unauthorized();

            var model = job.Applicants.Select(a => new ApplicantViewModel
            {
                UserId = a.UserId,
                Email = a.User.Email,
                Feedback = a.User.Feedbacks.FirstOrDefault(f => f.JobId == jobId)?.FeedbackText
            });

            return View("~/Views/User/HR/Applicants.cshtml", model);
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

            if (job == null)
                return NotFound();

            _jobRepo.Delete(jobId);

            return RedirectToAction("JobListings");
        }
    }
}
