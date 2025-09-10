using DotnetMVCApp.Repositories;
using DotnetMVCApp.ViewModels.HR;
using DotnetMVCApp.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DotnetMVCApp.Controllers
{
    public class HRController : Controller
    {
        private readonly IJobRepo _jobRepo;
        private readonly IUserJobRepo _userJobRepo;
        private readonly IUserRepo _userRepo;
        private readonly IInterviewrepo _interviewRepo;

        public HRController(IJobRepo jobRepo, IUserJobRepo userJobRepo, IUserRepo userRepo, IInterviewrepo interviewRepo)
        {
            _jobRepo = jobRepo;
            _userJobRepo = userJobRepo;
            _userRepo = userRepo;
            _interviewRepo = interviewRepo;
        }

        private int GetCurrentHrId()
        {
            // Replace with real authentication
            //return int.Parse(User.FindFirst("UserId").Value);
            return 1;
        }

        public IActionResult Overview()
        {
            int hrId = GetCurrentHrId();

            var jobs = _jobRepo.GetAllJobs().Where(j => j.PostedByUserId == hrId).ToList();
            var applicants = jobs.SelectMany(j => j.Applicants).Count();
            var interviews = _interviewRepo.GetAllInterview()
                                           .Where(i => i.Job.PostedByUserId == hrId )
                                           .Count();

            var model = new OverviewViewModel
            {
                ActiveJobs = jobs.Count(j => j.CloseTime > DateTime.Now),
                TotalApplicants = applicants,
                UpcomingInterviews = interviews
            };

            return View("~/Views/User/HR/Overview.cshtml", model);
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

            var job = new Job
            {
                JobTitle = model.JobTitle,   // ✅ set JobTitle
                JobDescription = model.JobDescription,
                TechStacks = model.TechStacks,
                SkillsRequired = JsonSerializer.Serialize(
                    model.SkillsRequired?
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ),
                OpenTime = DateTime.SpecifyKind(model.OpenTime, DateTimeKind.Utc),
                CloseTime = DateTime.SpecifyKind(model.CloseTime, DateTimeKind.Utc),
                PostedByUserId = GetCurrentHrId()
            };

            _jobRepo.Add(job);
            return RedirectToAction("JobListings");
        }




        public IActionResult JobListings()
        {
            int hrId = GetCurrentHrId();

            var jobs = _jobRepo.GetAllJobs().Where(j => j.PostedByUserId == hrId).ToList();

            var model = jobs.Select(j => new JobListingViewModel
            {
                JobId = j.JobId,
                JobTitle = j.JobTitle,             // ✅ include JobTitle
                JobDescription = j.JobDescription,
                TechStacks = j.TechStacks,
                OpenTime = j.OpenTime,
                CloseTime = j.CloseTime,
                Status = j.CloseTime > DateTime.Now ? "Active" : "Closed"
            });

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
        public IActionResult EditJob(int jobId)
        {
            var job = _jobRepo.GetJobById(jobId);
            if (job == null) return NotFound();

            var model = new CreateJobViewModel
            {
                JobId = job.JobId,
                JobTitle = job.JobTitle,
                JobDescription = job.JobDescription,
                TechStacks = job.TechStacks,
                SkillsRequired = string.Join(", ",
                    JsonSerializer.Deserialize<List<string>>(job.SkillsRequired ?? "[]") ?? new List<string>()),
                OpenTime = job.OpenTime,
                CloseTime = job.CloseTime
            };

            return View("~/Views/User/HR/EditJob.cshtml", model);
        }


        [HttpPost]
        public IActionResult EditJob(int jobId, CreateJobViewModel model)
        {


            if (!ModelState.IsValid)
                return View("~/Views/User/HR/EditJob.cshtml", model);

            var job = _jobRepo.GetJobById(jobId);

            // TEMP: Disable Unauthorized until authentication is real
            if (job == null /* || job.PostedByUserId != GetCurrentHrId() */)
                return NotFound();

            job.JobTitle = model.JobTitle;
            job.JobDescription = model.JobDescription;
            job.TechStacks = model.TechStacks;
            job.SkillsRequired = JsonSerializer.Serialize(
                model.SkillsRequired?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            );
            job.OpenTime = DateTime.SpecifyKind(model.OpenTime, DateTimeKind.Utc);
            job.CloseTime = DateTime.SpecifyKind(model.CloseTime, DateTimeKind.Utc);

            _jobRepo.Update(job);

            return RedirectToAction("JobListings"); // ✅ fixed
        }

        [HttpPost]
        public IActionResult DeleteJob(int jobId)
        {
            Console.WriteLine(jobId);
            var job = _jobRepo.GetJobById(jobId);

            // TEMP: Disable Unauthorized until authentication is real
            if (job == null /* || job.PostedByUserId != GetCurrentHrId() */)
                return NotFound();

            _jobRepo.Delete(jobId);

            return RedirectToAction("JobListings"); // ✅ fixed
        }



    }
}
