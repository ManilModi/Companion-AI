namespace DotnetMVCApp.ViewModels.Candidate
{
    public class CustomJobSearchRequest
    {
        public string Query { get; set; } = string.Empty;
    }

    public class JobSearchAIViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
    }

    public class JobSearchAIResponse
    {
        public List<JobSearchAIViewModel> Jobs { get; set; } = new();
    }

}
