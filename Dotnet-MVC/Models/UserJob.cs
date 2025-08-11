namespace DotnetMVCApp.Models
{
    public class UserJob
    {
        public int UserId { get; set; }
        public AppUser User { get; set; }

        public int JobId { get; set; }
        public Job Job { get; set; }

        [Column(TypeName = "jsonb")]
        public string Score { get; set; }
    }
}
