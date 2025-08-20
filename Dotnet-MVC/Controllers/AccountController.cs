using Microsoft.AspNetCore.Mvc;

public class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        return View(); // will look for Views/Account/Login.cshtml
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(); // will look for Views/Account/Register.cshtml
    }

    [HttpPost]
    public IActionResult Login(string Username, string Password, bool RememberMe)
    {
        // TODO: validate user
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Register(string Username, string Email, string Password)
    {
        // TODO: save user
        return RedirectToAction("Login");
    }
}
