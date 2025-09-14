using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DotnetMVCApp.Models;
using DotnetMVCApp.Attributes;
using DotnetMVCApp.ViewModels;
using HiringAssistance.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace DotnetMVCApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepo _userRepo;
        private readonly EmailService _emailService; // Replace IResend with EmailService

        public AccountController(IUserRepo userRepo, EmailService emailService)
        {
            _userRepo = userRepo;
            _emailService = emailService;
        }

        // ---------- Utility Methods ----------
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

        private string GenerateOtp(int length = 6)
        {
            var random = new Random();
            var otp = new StringBuilder();
            for (int i = 0; i < length; i++)
                otp.Append(random.Next(0, 10));
            return otp.ToString();
        }

        // ---------------- Register ----------------
        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (_userRepo.GetUserByEmail(model.Email) != null)
            {
                ModelState.AddModelError("", "Email already registered");
                return View(model);
            }

            string salt = GenerateSalt();
            string hashedPassword = HashPassword(model.Password, salt);

            var roleValue = model.Role switch
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

        // ---------------- Login ----------------
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = _userRepo.GetUserByEmail(model.Email);
            if (user == null || HashPassword(model.Password, user.Salt) != user.Password)
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View(model);
            }

            // Session
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", user.Role.ToString());

            // Cookie Authentication
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
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(15)
            };

            await HttpContext.SignInAsync("MyCookieAuth",
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return user.Role switch
            {
                UserRole.HR => RedirectToAction("Overview", "HR"),
                UserRole.Candidate => RedirectToAction("Dashboard", "Candidate"),
                _ => RedirectToAction("Login")
            };
        }

        // ---------------- Logout ----------------
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync("MyCookieAuth");
            return RedirectToAction("Index", "Home");
        }

        // ---------------- Forgot Password ----------------
        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = _userRepo.GetUserByEmail(email);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found");
                return View();
            }

            var otp = GenerateOtp();
            HttpContext.Session.SetString("ForgotPasswordOtp", otp);
            HttpContext.Session.SetString("ForgotPasswordEmail", email);
            HttpContext.Session.SetString("OtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString());

            // ---------------- Send OTP via Gmail SMTP ----------------
            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "Your OTP Code",
                    $"<p>Your OTP is <strong>{otp}</strong>. It expires in 10 minutes.</p>"
                );
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to send OTP. Please try again.");
                Console.WriteLine(ex.Message);
                return View();
            }

            return RedirectToAction("VerifyOtp");
        }

        // ---------------- Verify OTP ----------------
        [HttpGet]
        public IActionResult VerifyOtp()
        {
            if (HttpContext.Session.GetString("ForgotPasswordOtp") == null)
            {
                TempData["Error"] = "Please request an OTP first.";
                return RedirectToAction("ForgotPassword");
            }
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        public IActionResult VerifyOtp(ForgotPasswordViewModel model)
        {
            var storedOtp = HttpContext.Session.GetString("ForgotPasswordOtp");
            var email = HttpContext.Session.GetString("ForgotPasswordEmail");
            var expiryString = HttpContext.Session.GetString("OtpExpiry");

            if (storedOtp == null || email == null || expiryString == null)
            {
                TempData["Error"] = "OTP session expired. Try again.";
                return View(model);
            }

            if (DateTime.UtcNow > DateTime.Parse(expiryString))
            {
                TempData["Error"] = "OTP expired. Request a new one.";
                return View(model);
            }

            if (model.OTP != storedOtp)
            {
                TempData["Error"] = "Invalid OTP.";
                return View(model);
            }

            HttpContext.Session.Remove("ForgotPasswordOtp");
            HttpContext.Session.Remove("OtpExpiry");
            TempData["Email"] = email;

            return RedirectToAction("ResetPassword", new { email });
        }

        // ---------------- Reset Password ----------------
        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");
            return View(new ResetPasswordViewModel { Email = email });
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var email = HttpContext.Session.GetString("ForgotPasswordEmail");
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Session expired. Try again.");
                return View(model);
            }

            var user = _userRepo.GetUserByEmail(email);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            var salt = GenerateSalt();
            user.Password = HashPassword(model.NewPassword, salt);
            user.Salt = salt;
            _userRepo.Update(user);

            HttpContext.Session.Remove("ForgotPasswordEmail");
            TempData["Message"] = "Password reset successfully! You can now log in.";
            return RedirectToAction("Login");
        }
    }
}
