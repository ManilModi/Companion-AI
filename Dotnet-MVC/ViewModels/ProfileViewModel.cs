using System.ComponentModel.DataAnnotations;
using DotnetMVCApp.Attributes;

namespace DotnetMVCApp.ViewModels
{
    public class ProfileViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }

    }
}
