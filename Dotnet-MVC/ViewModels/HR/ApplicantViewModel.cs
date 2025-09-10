using System;

namespace DotnetMVCApp.ViewModels.HR
{
    public class ApplicantViewModel
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public DateTime AppliedOn { get; set; }
        public string Feedback { get; set; }
    }
}
