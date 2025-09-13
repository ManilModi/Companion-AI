using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DotnetMVCApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userRole = HttpContext.Session.GetString("UserRole");

            // Check cookie authentication if session is missing
            if ((!userId.HasValue || string.IsNullOrEmpty(userRole)) && User.Identity?.IsAuthenticated == true)
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                var roleClaim = User.FindFirst(ClaimTypes.Role);

                if (idClaim != null && roleClaim != null)
                {
                    if (int.TryParse(idClaim.Value, out int parsedUserId))
                        userId = parsedUserId;

                    userRole = roleClaim.Value;

                    // Store back in session for convenience
                    HttpContext.Session.SetInt32("UserId", userId.Value);
                    HttpContext.Session.SetString("UserRole", userRole);
                }
            }

            if (userId.HasValue && !string.IsNullOrEmpty(userRole))
            {
                return userRole switch
                {
                    "HR" => RedirectToAction("Overview", "HR"),
                    "Candidate" => RedirectToAction("Dashboard", "Candidate"),
                    _ => View() // fallback
                };
            }

            // Not logged in
            return View();
        }
    }
}
