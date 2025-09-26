using System.ComponentModel.DataAnnotations;
using DotnetMVCApp.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace DotnetMVCApp.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address (e.g. user@example.com)")]
        [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StrongPassword] // custom validation
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;
    }
}
