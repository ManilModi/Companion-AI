using System;
using System.ComponentModel.DataAnnotations;

namespace DotnetMVCApp.ViewModels.HR
{
    public class CreateJobViewModel
    {
        public int JobId { get; set; }

        [Required]
        public string JobTitle { get; set; }

        [Required]
        public string JobDescription { get; set; }

        [Required]
        public string TechStacks { get; set; }

        [Required]
        public string SkillsRequired { get; set; }

        [Required]
        public DateTime OpenTime { get; set; }

        [Required]
        public DateTime CloseTime { get; set; }

        // ✅ New fields
        [Required]
        public string Company { get; set; }

        [Required]
        public string Location { get; set; }

        [Required]
        public string JobType { get; set; }

        [Required]
        public string SalaryRange { get; set; }
    }
}
