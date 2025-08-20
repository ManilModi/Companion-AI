using Microsoft.AspNetCore.Mvc;

public class AccountController : Controller
{
    [HttpGet]
    public IActionResult SignIn()
    {
        return View(); // will look for Views/Account/SignIn.cshtml
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(); // will look for Views/Account/Register.cshtml
    }

    [HttpPost]
    public IActionResult SignIn(string Email, string Password)
    {
        // TODO: validate user
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Register(string Username, string Email, string Password)
    {
        // TODO: save user
        return RedirectToAction("SignIn");
    }


}
