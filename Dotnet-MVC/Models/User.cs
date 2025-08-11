using HiringAssistance.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace DotnetMVCApp.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? ClerkId { get; set; }

        public string? ResumeUrl { get; set; } // Cloudinary

        public string? BadgesUrl { get; set; } // Cloudinary

        [Column(TypeName = "jsonb")]
        public string? ExtractedInfo { get; set; }

        [Required]
        public UserRole Role { get; set; }

        public ICollection<Job>? JobsPosted { get; set; }
        public ICollection<UserJob>? JobsApplied { get; set; }

        public ICollection<Interview>? Interviews { get; set; }
        public ICollection<Feedback>? Feedbacks { get; set; }
    }
}
