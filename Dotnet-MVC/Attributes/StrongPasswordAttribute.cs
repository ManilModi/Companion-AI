using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DotnetMVCApp.Attributes
{
    public class StrongPasswordAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var password = value as string;
            if (string.IsNullOrWhiteSpace(password))
                return new ValidationResult("Password is required");

            if (password.Length < 8)
                return new ValidationResult("Password must be at least 8 characters long");

            if (!Regex.IsMatch(password, "[A-Z]"))
                return new ValidationResult("Password must contain at least 1 uppercase letter");

            if (!Regex.IsMatch(password, "[a-z]"))
                return new ValidationResult("Password must contain at least 1 lowercase letter");

            if (!Regex.IsMatch(password, "[0-9]"))
                return new ValidationResult("Password must contain at least 1 digit");

            if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
                return new ValidationResult("Password must contain at least 1 special character");

            return ValidationResult.Success!;
        }
    }

}
