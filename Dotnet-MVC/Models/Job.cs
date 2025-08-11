using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotnetMVCApp.Models
{
    public class Job
    {
        [Key]
        public int JobId { get; set; }

        [Required]
        public string JobDescription { get; set; }

        public string TechStacks { get; set; }

        [Column(TypeName = "jsonb")]
        public string SkillsRequired { get; set; }

        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }

        [Column(TypeName = "jsonb")]
        public string Quiz { get; set; }

        public ICollection<UserJob> Applicants { get; set; }

        public int PostedByUserId { get; set; }
        public AppUser PostedBy { get; set; }

        public ICollection<Interview> Interviews { get; set; }
        public ICollection<Feedback> Feedbacks { get; set; }
    }
}
