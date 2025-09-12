
namespace DotnetMVCApp.ViewModels.HR
{
    public class ApplicantViewModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string ContactNo { get; set; } = "";
        public string ResumeUrl { get; set; } = "";
        public List<string> Skills { get; set; } = new();
        public string ExperienceSummary { get; set; } = "";
        public int? TotalExperienceYears { get; set; }
        public List<string> ProjectsBuilt { get; set; } = new();
        public List<string> Achievements { get; set; } = new();
        public Dictionary<string, int>? Scores { get; set; }
    }
}