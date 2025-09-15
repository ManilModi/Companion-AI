using System.ComponentModel.DataAnnotations;

namespace DotnetMVCApp.Models
{
    public class Feedback
    {
        [Key]
        public int FeedbackId { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }


        public int JobId { get; set; }
        public Job? Job { get; set; }

        // Store Cloudinary URL instead of full text
        public string? FeedbackUrl { get; set; }
        public string? Sentiment { get; set; }
    }
}

