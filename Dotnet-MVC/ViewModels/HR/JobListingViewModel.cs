using System;

namespace DotnetMVCApp.ViewModels.HR
{
    public class JobListingViewModel
    {
        public int JobId { get; set; }
        public string JobTitle { get; set; }       // ✅ New
        public string JobDescription { get; set; }
        public string TechStacks { get; set; }
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }
        public string Status { get; set; }
    }


}
