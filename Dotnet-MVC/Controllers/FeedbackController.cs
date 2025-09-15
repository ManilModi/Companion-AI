using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DocumentFormat.OpenXml.Packaging;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using DotnetMVCApp.ViewModels.Feedback;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace DotnetMVCApp.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly IFeedbackrepo _feedbackRepo;
        private readonly IJobRepo _jobRepo;
        private readonly Cloudinary _cloudinary;

        public FeedbackController(IFeedbackrepo feedbackRepo, IJobRepo jobRepo, Cloudinary cloudinary)
        {
            _feedbackRepo = feedbackRepo;
            _jobRepo = jobRepo;
            _cloudinary = cloudinary;
        }

        // ---------------- HR ONLY ----------------

        [Authorize(Roles = "HR")]
        public async Task<IActionResult> JobFeedbacks(int jobId)
        {
            // Get all feedbacks for this job
            var feedbackEntities = _feedbackRepo.GetByJob(jobId).ToList();

            // Get job info for JobTitle
            var job = _jobRepo.GetJobById(jobId);
            string jobTitle = job?.JobTitle ?? "[Unknown Job]";

            // Map to FeedbackViewModel
            var feedbacks = new List<FeedbackViewModel>();
            foreach (var f in feedbackEntities)
            {
                // Optional: fetch FeedbackText if stored as URL or file
                string feedbackText = await GetFeedbackTextAsync(f.FeedbackUrl);

                feedbacks.Add(new FeedbackViewModel
                {
                    FeedbackId = f.FeedbackId,
                    JobId = f.JobId,
                    JobTitle = jobTitle,
                    UserId = f.UserId,
                    UserEmail = f.User?.Email,
                    FeedbackUrl = f.FeedbackUrl,
                    FeedbackText = feedbackText
                });
            }

            return View("HR/JobFeedbacks", feedbacks);
        }


        // ---------------- CANDIDATE ONLY ----------------

        [Authorize(Roles = "Candidate")]
        public async Task<IActionResult> JobFeedbacksCandidate(int jobId)
        {
            int userId = GetCurrentUserId();

            // Get all feedbacks for this job
            var feedbackEntities = _feedbackRepo.GetByJob(jobId).ToList();

            // Get job info for JobTitle
            var job = _jobRepo.GetJobById(jobId);
            string jobTitle = job?.JobTitle ?? "[Unknown Job]";

            // Map to FeedbackViewModel
            var feedbacks = new List<FeedbackViewModel>();
            foreach (var f in feedbackEntities)
            {
                string feedbackText = await GetFeedbackTextAsync(f.FeedbackUrl);

                feedbacks.Add(new FeedbackViewModel
                {
                    FeedbackId = f.FeedbackId,
                    JobId = f.JobId,
                    JobTitle = jobTitle,
                    UserId = f.UserId,
                    UserEmail = f.User?.Email,
                    FeedbackUrl = f.FeedbackUrl,
                    FeedbackText = feedbackText
                });
            }

            // Check if current candidate has submitted feedback
            var userFeedback = _feedbackRepo.GetByUserAndJob(userId, jobId);
            ViewBag.JobId = jobId;
            ViewBag.UserHasFeedback = userFeedback != null;

            // ✅ Return candidate-specific JobFeedbacks view
            return View("~/Views/Feedback/Candidate/JobFeedbacks.cshtml", feedbacks);
        }


        [Authorize(Roles = "Candidate")] 
        public IActionResult Create(int jobId) 
        { 
            var vm = new FeedbackViewModel { JobId = jobId }; 
            return View("Candidate/Create", vm); 
        }

        [HttpPost]
        [Authorize(Roles = "Candidate")]
        public IActionResult Create(FeedbackViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("[Create] Model state invalid.");
                return View("Candidate/Create", model);
            }

            int userId = GetCurrentUserId();
            Console.WriteLine($"[Create] Current user ID: {userId}");

            // Check for duplicate feedback
            var existing = _feedbackRepo.GetByUserAndJob(userId, model.JobId);
            if (existing != null)
            {
                Console.WriteLine("[Create] Feedback already exists for this user and job.");
                ModelState.AddModelError("", "You have already submitted feedback for this job.");
                return View("Candidate/Create", model);
            }

            // Upload feedback to Cloudinary
            string url = UploadFeedbackToCloudinary(model.FeedbackText, $"feedback_user{userId}_job{model.JobId}");

            var feedback = new Feedback
            {
                JobId = model.JobId,
                UserId = userId,
                FeedbackUrl = url,
                Sentiment = null
            };

            _feedbackRepo.Add(feedback);

            Console.WriteLine($"[Create] Feedback added for JobId {model.JobId}, UserId {userId}, URL: {url}");

            return RedirectToAction("JobFeedbacksCandidate", "Feedback", new { jobId = model.JobId });
        }

        // ---------------- Cloudinary Helper ----------------
        private string UploadFeedbackToCloudinary(string text, string fileName)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var bytes = Encoding.UTF8.GetBytes(text);
            using var stream = new MemoryStream(bytes);

            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName + ".txt", stream),
                Folder = "Feedbacks"
            };

            var result = _cloudinary.Upload(uploadParams);

            Console.WriteLine($"[Cloudinary] Uploaded file '{fileName}' -> URL: {result.SecureUrl}");

            return result.SecureUrl?.ToString();
        }



        private async Task<string> GetFeedbackTextAsync(string feedbackUrl)
        {
            if (string.IsNullOrEmpty(feedbackUrl))
                return "[No feedback submitted]";

            try
            {
                using var httpClient = new HttpClient();
                var fileBytes = await httpClient.GetByteArrayAsync(feedbackUrl);

                if (feedbackUrl.EndsWith(".txt"))
                {
                    return Encoding.UTF8.GetString(fileBytes);
                }
                else if (feedbackUrl.EndsWith(".docx"))
                {
                    using var ms = new MemoryStream(fileBytes);
                    using var doc = WordprocessingDocument.Open(ms, false);
                    return string.Join(" ",
                        doc.MainDocumentPart.Document.Body
                           .Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                           .Select(p => p.InnerText));
                }
                else if (feedbackUrl.EndsWith(".pdf"))
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
                return $"[Error fetching feedback: {ex.Message}]";
            }
        }

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

    }
}
