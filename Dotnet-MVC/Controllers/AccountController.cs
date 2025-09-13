using System.Security.Cryptography;
using System.Text;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using HiringAssistance.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

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
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    private string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var combined = Encoding.UTF8.GetBytes(password + salt);
        var hashBytes = sha256.ComputeHash(combined);
        return Convert.ToBase64String(hashBytes);
    }

    // --------- Register ----------
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

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

        UserRole roleValue = model.Role switch
        {
            "HR" => UserRole.HR,
            "Candidate" => UserRole.Candidate,
            _ => UserRole.Candidate
        };

        var user = new User
        {
            Username = model.Username,
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
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = _userRepo.GetUserByEmail(model.Email);
        if (user == null || HashPassword(model.Password, user.Salt) != user.Password)
        {
            ModelState.AddModelError("", "Invalid email or password");
            return View(model);
        }

        // --- Store user info in session ---
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("UserEmail", user.Email);
        HttpContext.Session.SetString("UserRole", user.Role.ToString());

        // --- Store user info in cookie authentication ---
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // keeps user logged in across browser restarts
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(15)
        };

        await HttpContext.SignInAsync("MyCookieAuth",
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // Redirect based on role
        return user.Role switch
        {
            UserRole.HR => RedirectToAction("Overview", "HR"),
            UserRole.Candidate => RedirectToAction("Dashboard", "Candidate"),
            _ => RedirectToAction("Login")
        };
    }

    // --------- Logout ----------
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear(); // clear all session data
        await HttpContext.SignOutAsync("MyCookieAuth"); // clear authentication cookie
        return RedirectToAction("Index", "Home"); // redirect to Home/Index
    }


    // --------- Access Denied ----------
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
