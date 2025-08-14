using Microsoft.AspNetCore.Mvc;

namespace DotnetMVCApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("SyncUser", "User");
            }

            return View();
        }
    }
}
