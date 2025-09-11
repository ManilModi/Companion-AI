namespace DotnetMVCApp.ViewModels.Candidate
{
    public class JobSearchViewModel
    {
        public int JobId { get; set; }
        public string JobTitle { get; set; }
        public string Company { get; set; }
        public string Location { get; set; }
        public string JobType { get; set; }
        public string SalaryRange { get; set; }
        public DateTime PostedDate { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public int ApplicantsCount { get; set; }
    }
}
