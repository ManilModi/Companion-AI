using System.ComponentModel.DataAnnotations;
using DotnetMVCApp.Attributes;

namespace DotnetMVCApp.ViewModels
{
    public class UpdateUserViewModel
    {
        public int UserId { get; set; }
        [Required, StringLength(50)]
        public string Username { get; set; }

        [DataType(DataType.Password)]
        [StrongPassword] // custom validation
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}
