namespace DotnetMVCApp.ViewModels.Candidate
{
    public class MockInterviewViewModel
    {
        public int JobId { get; set; }
        public string JobDescription { get; set; } = string.Empty;

        public string? ScoreJson { get; set; }
    }
}
