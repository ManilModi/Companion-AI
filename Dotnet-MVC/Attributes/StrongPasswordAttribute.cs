using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DotnetMVCApp.Attributes
{
    public class StrongPasswordAttribute : ValidationAttribute, IClientModelValidator
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

        // This makes it work with jQuery Validation
        public void AddValidation(ClientModelValidationContext context)
        {
            MergeAttribute(context.Attributes, "data-val", "true");
            MergeAttribute(context.Attributes, "data-val-strongpassword",
                "Password must be at least 8 characters long, contain uppercase, lowercase, digit, and special character.");
        }

        private bool MergeAttribute(IDictionary<string, string> attributes, string key, string value)
        {
            if (attributes.ContainsKey(key)) return false;
            attributes.Add(key, value);
            return true;
        }
    }
}
