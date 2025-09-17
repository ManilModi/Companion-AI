using System.ComponentModel.DataAnnotations;

namespace DotnetMVCApp.ViewModels
{
    public class UpdateUserViewModel
    {
        public int UserId { get; set; }

        [Required, StringLength(50)]
        public string Username { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }
    }
}
