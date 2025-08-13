using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotnetMVCApp.Models
{
    public class UserJob
    {
        public int UserId { get; set; }
<<<<<<< HEAD

=======
>>>>>>> 82ffdd3f89b313db68f67e7694ccdf551fa98451
        public User? User { get; set; }
        public int JobId { get; set; }
        public Job? Job { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Score { get; set; }
    }
}
