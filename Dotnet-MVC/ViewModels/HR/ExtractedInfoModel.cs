using System.Text.Json.Serialization;

namespace DotnetMVCApp.ViewModels.HR
{
    public class ExtractedInfoModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("contact_no")]
        public string ContactNo { get; set; }

        [JsonPropertyName("experience")]
        public string ExperienceSummary { get; set; }

        [JsonPropertyName("total_experience_years")]
        public double? TotalExperienceYears { get; set; }

        [JsonPropertyName("projects_built")]
        public List<string> ProjectsBuilt { get; set; } = new();

        [JsonPropertyName("achievements_like_awards_and_certifications")]
        public List<string> Achievements { get; set; } = new();

        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = new();
    }
}
