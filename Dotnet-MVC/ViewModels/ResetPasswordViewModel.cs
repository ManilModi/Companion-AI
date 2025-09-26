using System.ComponentModel.DataAnnotations;
using DotnetMVCApp.Attributes;

namespace DotnetMVCApp.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }   // ✅ add this

        [Required, DataType(DataType.Password)]
        [StrongPassword] // custom validation
        public string NewPassword { get; set; }

        [Required, DataType(DataType.Password), Compare("NewPassword")]
        public string ConfirmPassword { get; set; }
    }
}
