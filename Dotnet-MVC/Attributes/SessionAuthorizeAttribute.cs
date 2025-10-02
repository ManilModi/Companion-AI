using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace DotnetMVCApp.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SessionAuthorizeAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string? _role;
        private readonly string? _idParameterName;

        /// role: optional, only allow users with this role
        /// idParameterName: optional, the action parameter that should match the logged-in user's ID
        public SessionAuthorizeAttribute(string? role = null, string? idParameterName = null)
        {
            _role = role;
            _idParameterName = idParameterName;
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

            // --- Role check with multiple roles ---
            if (!string.IsNullOrEmpty(_role))
            {
                var allowedRoles = _role.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(r => r.Trim())
                                        .ToArray();

                if (!allowedRoles.Any(r => string.Equals(r, userRole, StringComparison.OrdinalIgnoreCase)))
                {
                    context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                    return;
                }
            }


            // --- User ID check: only allow access to their own data ---
            if (!string.IsNullOrEmpty(_idParameterName))
            {
                if (context.ActionArguments.TryGetValue(_idParameterName, out var actionIdObj))
                {
                    if (actionIdObj is int actionId)
                    {
                        if (actionId != userId.Value)
                        {
                            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                            return;
                        }
                    }
                }
            }

            // --- Authorized, proceed ---
            await next();
        }
    }
}
