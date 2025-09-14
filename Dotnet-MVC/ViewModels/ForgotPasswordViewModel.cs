using System.ComponentModel.DataAnnotations;

namespace DotnetMVCApp.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        public string OTP { get; set; }
    }

}
