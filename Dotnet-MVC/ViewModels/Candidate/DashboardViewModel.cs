namespace DotnetMVCApp.ViewModels.Candidate
{
    public class DashboardViewModel
    {
        public int UserId { get; set; }

        // From JSON
        public string Name { get; set; }
        public string Email { get; set; }
        public string? ContactNo { get; set; }
        public string? LinkedInProfileLink { get; set; }

        public string? ResumeUrl { get; set; }
        public string? BadgesUrl { get; set; }

        public List<string> Skills { get; set; } = new();

        // Experience can be single string OR a list of objects
        public string? ExperienceSummary { get; set; }
        public double? TotalExperienceYears { get; set; }

        public List<string> ProjectsBuilt { get; set; } = new();
        public List<string> Achievements { get; set; } = new();
    }
}
