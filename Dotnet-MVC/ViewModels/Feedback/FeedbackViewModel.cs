using System.ComponentModel.DataAnnotations;

namespace DotnetMVCApp.ViewModels.Feedback
{
    public class FeedbackViewModel
    {
        public int FeedbackId { get; set; }
        public int JobId { get; set; }
        public string? JobTitle { get; set; }
        public int UserId { get; set; }
        public string? UserEmail { get; set; }

        public string? FeedbackUrl { get; set; }

        [Required(ErrorMessage = "Feedback text is required.")]
        public string FeedbackText { get; set; }
    }


}
