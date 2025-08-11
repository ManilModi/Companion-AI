using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotnetMVCApp.Models
{
    public class Interview
    {
        [Key]
        public int InterviewId { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int JobId { get; set; }
        public Job? Job { get; set; }

        public string? VideoUrl { get; set; } // Cloudinary
        public string? AudioUrl { get; set; } // Cloudinary

        [Column(TypeName = "jsonb")]
        public string? Score { get; set; }
    }
}

