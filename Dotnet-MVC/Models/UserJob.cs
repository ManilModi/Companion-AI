using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotnetMVCApp.Models
{
    public class UserJob
    {
        public int UserId { get; set; }


        public User? User { get; set; }

        public int JobId { get; set; }
        public Job? Job { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Score { get; set; }
    }
}
