using System.ComponentModel.DataAnnotations;

namespace DotnetMVCApp.Models
{
    public class Feedback
    {
        [Key]
        public int FeedbackId { get; set; }

        public int UserId { get; set; }
        public AppUser User { get; set; }

        public int JobId { get; set; }
        public Job Job { get; set; }

        public string FeedbackText { get; set; }
        public string Sentiment { get; set; }
    }
}

