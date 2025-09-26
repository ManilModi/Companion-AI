namespace DotnetMVCApp.ViewModels.Candidate
{
    public class MockInterviewHistoryViewModel
    {
        public int InterviewId { get; set; }
        public int JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string? Score { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
