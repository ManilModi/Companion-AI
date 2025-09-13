using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace DotnetMVCApp.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SessionAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string? _role;

        // Role is optional: if null, any authenticated user can access
        public SessionAuthorizeAttribute(string? role = null)
        {
            _role = role;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            // --- Try to get from session first ---
            int? userId = httpContext.Session.GetInt32("UserId");
            string? userRole = httpContext.Session.GetString("UserRole");

            // --- Fallback to cookie claims if session is empty ---
            if (!userId.HasValue || string.IsNullOrEmpty(userRole))
            {
                var idClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                var roleClaim = httpContext.User.FindFirst(ClaimTypes.Role);

                if (idClaim != null && int.TryParse(idClaim.Value, out int parsedUserId))
                    userId = parsedUserId;

                if (roleClaim != null)
                    userRole = roleClaim.Value;

                // Restore session for convenience
                if (userId.HasValue)
                    httpContext.Session.SetInt32("UserId", userId.Value);
                if (!string.IsNullOrEmpty(userRole))
                    httpContext.Session.SetString("UserRole", userRole);
            }

            // --- If still not logged in, redirect to login ---
            if (!userId.HasValue)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // --- If role is specified, enforce it ---
            if (!string.IsNullOrEmpty(_role) && !string.Equals(userRole, _role, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            // --- Authorized, proceed ---
            await next();
        }
    }
}
