using System;
using System.ComponentModel.DataAnnotations;

namespace DotnetMVCApp.ViewModels.HR
{
    public class CreateJobViewModel
    {
        [Required]
        public string JobTitle { get; set; }   // ✅ New

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
    }

}
