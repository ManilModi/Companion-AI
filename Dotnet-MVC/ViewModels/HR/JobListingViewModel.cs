using System;

namespace DotnetMVCApp.ViewModels.HR
{
    public class JobListingViewModel
    {
        public int JobId { get; set; }
        public string JobTitle { get; set; }
        public string JobDescription { get; set; }
        public string TechStacks { get; set; }
        public string SkillsRequired { get; set; }
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public string Status { get; set; }

        // ✅ Newly added fields
        public string Company { get; set; }
        public string Location { get; set; }
        public string JobType { get; set; }
        public string SalaryRange { get; set; }
    }
}
