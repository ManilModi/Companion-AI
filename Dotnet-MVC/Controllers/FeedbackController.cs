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
                    FeedbackText = feedbackText,
                    Sentiment = f.Sentiment
                });
            }
            double? avgSentiment = null;
            if (feedbackEntities.Count(f => f.Sentiment.HasValue) >= 10)
            {
                avgSentiment = feedbackEntities
                    .Where(f => f.Sentiment.HasValue)
                    .Average(f => f.Sentiment.Value) * 100;
            }

            ViewBag.AvgSentimentPercent = avgSentiment;
            ViewBag.JobTitle = jobTitle;


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
            Console.WriteLine("Job title "+job?.JobTitle);
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
                    FeedbackText = feedbackText,
                    Sentiment = f.Sentiment
                });
            }

            double? avgSentiment = null;
            if (feedbackEntities.Count(f => f.Sentiment.HasValue) >= 10)
            {
                avgSentiment = feedbackEntities
                    .Where(f => f.Sentiment.HasValue)
                    .Average(f => f.Sentiment.Value) * 100;
            }

            var userFeedback = _feedbackRepo.GetByUserAndJob(userId, jobId);

            ViewBag.JobId = jobId;
            ViewBag.JobTitle = jobTitle;
            ViewBag.UserHasFeedback = userFeedback != null;
            ViewBag.AvgSentimentPercent = avgSentiment;

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
        public async Task<IActionResult> Create(FeedbackViewModel model)
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

            // ✅ Call FastAPI for sentiment scoring
            int? sentimentScore = null;
            try
            {
                using var httpClient = new HttpClient();
                var requestData = new { feedback = model.FeedbackText };
                var json = System.Text.Json.JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("http://localhost:8000/analyze-feedback/", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonDocument.Parse(responseString);
                    sentimentScore = result.RootElement.GetProperty("sentiment_score").GetInt32();
                    Console.WriteLine($"[Create] Sentiment score from FastAPI: {sentimentScore}");
                }
                else
                {
                    Console.WriteLine($"[Create] FastAPI call failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Create] Error calling FastAPI: {ex.Message}");
            }

            var feedback = new Feedback
            {
                JobId = model.JobId,
                UserId = userId,
                FeedbackUrl = url,
                Sentiment = sentimentScore // store sentiment/score in DB
            };

            _feedbackRepo.Add(feedback);

            Console.WriteLine($"[Create] Feedback added for JobId {model.JobId}, UserId {userId}, URL: {url}");

            return RedirectToAction("JobFeedbacksCandidate", "Feedback", new { jobId = model.JobId });
        }

        [HttpPost]
        [Authorize(Roles = "Candidate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            int userId = GetCurrentUserId();
            var feedback = _feedbackRepo.GetFeedbackById(id);

            if (feedback == null || feedback.UserId != userId)
                return Forbid();

            int jobId = feedback.JobId;

            try
            {
                if (!string.IsNullOrEmpty(feedback.FeedbackUrl))
                {
                    Console.WriteLine($"[Delete] Original URL: {feedback.FeedbackUrl}");

                    var uri = new Uri(feedback.FeedbackUrl);
                    var path = uri.AbsolutePath;
                    Console.WriteLine($"[Delete] Uri.AbsolutePath: {path}");

                    var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine("[Delete] Path parts:");
                    for (int i = 0; i < parts.Length; i++)
                    {
                        Console.WriteLine($"   [{i}] {parts[i]}");
                    }

                    var uploadIndex = Array.IndexOf(parts, "upload");
                    if (uploadIndex == -1 || uploadIndex + 2 >= parts.Length)
                    {
                        Console.WriteLine("[Delete] ERROR: Could not locate 'upload' or version in URL. Skipping Cloudinary deletion.");
                    }
                    else
                    {
                        // Keep folder + file name with extension
                        var publicId = string.Join("/", parts.Skip(uploadIndex + 2)); // e.g., Feedbacks/feedback_user4_job11.txt
                        Console.WriteLine($"[Delete] Trying Cloudinary publicId: {publicId}");

                        var deletionParams = new DeletionParams(publicId)
                        {
                            ResourceType = ResourceType.Raw,
                            Invalidate = true
                        };

                        var deletionResult = await _cloudinary.DestroyAsync(deletionParams);
                        Console.WriteLine($"[Delete] Cloudinary delete result: {deletionResult.Result}, publicId used: {publicId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Delete] Error deleting from Cloudinary: {ex.Message}");
            }

            _feedbackRepo.Delete(id);
            Console.WriteLine($"[Delete] Feedback {id} deleted from DB for User {userId}, Job {jobId}");

            return RedirectToAction("JobFeedbacksCandidate", "Feedback", new { jobId });
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
                Folder = "Feedbacks",
                UseFilename = true,
                UniqueFilename = false,
                
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
