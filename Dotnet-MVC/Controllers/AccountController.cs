using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using HiringAssistance.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

public class AccountController : Controller
{
    private readonly IUserRepo _userRepo;

    public AccountController(IUserRepo userRepo)
    {
        _userRepo = userRepo;
    }

    // --------- Utility methods ----------
    private string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    private string HashPassword(string password, string salt)
    {
        using (var sha256 = SHA256.Create())
        {
            var combined = Encoding.UTF8.GetBytes(password + salt);
            var hashBytes = sha256.ComputeHash(combined);
            return Convert.ToBase64String(hashBytes);
        }
    }

    // --------- Register ----------
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (_userRepo.GetUserByEmail(model.Email) != null)
        {
            ModelState.AddModelError("", "Email already registered");
            return View(model);
        }

        string salt = GenerateSalt();
        string hashedPassword = HashPassword(model.Password, salt);

        // Map role string → int (keeps User.cs unchanged)
        UserRole roleValue = model.Role switch
        {
            "HR" => UserRole.HR,
            "Candidate" => UserRole.Candidate,
            _ => UserRole.Candidate
        };

        var user = new User
        {
            Email = model.Email,
            Password = hashedPassword,
            Salt = salt,
            Role = roleValue
        };

        _userRepo.Add(user);

        return RedirectToAction("Login");
    }

    // --------- Login ----------
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = _userRepo.GetUserByEmail(model.Email);
        if (user == null)
        {
            ModelState.AddModelError("", "Invalid email or password");
            return View(model);
        }

        string hashedInput = HashPassword(model.Password, user.Salt);
        if (hashedInput != user.Password)
        {
            ModelState.AddModelError("", "Invalid email or password");
            return View(model);
        }

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString())
    };

        var identity = new ClaimsIdentity(claims, "MyCookieAuth");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("MyCookieAuth", principal, new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = DateTime.UtcNow.AddHours(2)
        });

        return RedirectToAction("Index", "Home");
    }


    // --------- Logout ----------
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("MyCookieAuth");
        return RedirectToAction("Login");
    }

    // --------- Access Denied ----------
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
