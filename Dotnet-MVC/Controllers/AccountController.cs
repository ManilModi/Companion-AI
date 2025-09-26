using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DotnetMVCApp.Attributes;
using DotnetMVCApp.Models;
using DotnetMVCApp.ViewModels;
using HiringAssistance.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace DotnetMVCApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepo _userRepo;
        private readonly EmailService _emailService; 

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

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email); // Syntax check
                string domain = addr.Host;

                // Check MX records
                var entries = Dns.GetHostEntry(domain);
                return entries != null;
            }
            catch
            {
                return false;
            }
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
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (_userRepo.GetUserByEmail(model.Email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email already registered");
                return View(model);
            }

            if (!IsValidEmail(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Invalid or non-existent email domain.");
                return View(model);
            }
            // ✅ check if email exists using SMTP
            var emailExists = await _emailService.VerifyEmailAsync(model.Email);
            if (!emailExists)
            {
                ModelState.AddModelError(nameof(model.Email), "Email address does not exist or cannot be verified.");
                return View(model);
            }
            // Generate OTP
            var otp = GenerateOtp();
            HttpContext.Session.SetString("RegisterOtp", otp);
            HttpContext.Session.SetString("RegisterUser", System.Text.Json.JsonSerializer.Serialize(model));
            HttpContext.Session.SetString("RegisterOtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString());

            try
            {
                await _emailService.SendEmailAsync(
                    model.Email,
                    "Registration OTP",
                    $"<p>Your OTP is <strong>{otp}</strong>. It expires in 10 minutes.</p>" +
                    $"<p>If you did not request this code, please <a href='mailto:deepcoding15@gmail.com' style='color:#4CAF50;'>contact us</a> immediately.</p>\r\n" +
                    $"<p class='footer'>Thank you,<br/>Your Company Name</p>"
                );
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to send OTP. Try again.");
                Console.WriteLine(ex.Message);
                return View(model);
            }

            return RedirectToAction("VerifyRegisterOtp");
        }

        [HttpGet]
        public IActionResult VerifyRegisterOtp()
        {
            if (HttpContext.Session.GetString("RegisterOtp") == null)
            {
                TempData["Error"] = "Please register first.";
                return RedirectToAction("Register");
            }

            // Keep email visible for UX
            var userData = HttpContext.Session.GetString("RegisterUser");
            if (userData != null)
            {
                var regModel = System.Text.Json.JsonSerializer.Deserialize<RegisterViewModel>(userData);
                return View(new ForgotPasswordViewModel { Email = regModel.Email });
            }

            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> VerifyRegisterOtp(ForgotPasswordViewModel model)
        {
            var storedOtp = HttpContext.Session.GetString("RegisterOtp");
            var expiryString = HttpContext.Session.GetString("RegisterOtpExpiry");
            var userData = HttpContext.Session.GetString("RegisterUser");

            if (storedOtp == null || expiryString == null || userData == null)
            {
                TempData["Error"] = "OTP expired. Please register again.";
                return RedirectToAction("Register");
            }

            if (DateTime.UtcNow > DateTime.Parse(expiryString))
            {
                TempData["Error"] = "OTP expired. Please register again.";
                return RedirectToAction("Register");
            }

            if (model.OTP != storedOtp)
            {
                TempData["Error"] = "Invalid OTP.";
                return View(model);
            }

            // OTP correct -> complete registration
            var regModel = System.Text.Json.JsonSerializer.Deserialize<RegisterViewModel>(userData);

            string salt = GenerateSalt();
            string hashedPassword = HashPassword(regModel.Password, salt);

            var roleValue = regModel.Role switch
            {
                "HR" => UserRole.HR,
                "Candidate" => UserRole.Candidate,
                _ => UserRole.Candidate
            };

            var user = new User
            {
                Username = regModel.Username,
                Email = regModel.Email,
                Password = hashedPassword,
                Salt = salt,
                Role = roleValue
            };

            _userRepo.Add(user);

            // Clear registration session
            HttpContext.Session.Remove("RegisterOtp");
            HttpContext.Session.Remove("RegisterUser");
            HttpContext.Session.Remove("RegisterOtpExpiry");

            // ---------- Auto-login after registration ----------
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

            // Redirect based on role like login flow
            return user.Role switch
            {
                UserRole.HR => RedirectToAction("Overview", "HR"),
                UserRole.Candidate => RedirectToAction("Dashboard", "Candidate"),
                _ => RedirectToAction("Index", "Home")
            };
        }



        // ---------------- Login ----------------
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = _userRepo.GetUserByEmail(model.Email);
            if (user == null)
            {
                // Email not found
                ModelState.AddModelError(nameof(model.Email), "Email not found");
                return View(model);
            }

            if (HashPassword(model.Password, user.Salt) != user.Password)
            {
                // Password incorrect
                ModelState.AddModelError(nameof(model.Password), "Incorrect password");
                model.Password = "";
                return View(model);
            }

            // Generate OTP
            var otp = GenerateOtp();
            HttpContext.Session.SetString("LoginOtp", otp);
            HttpContext.Session.SetString("LoginUserId", user.UserId.ToString());
            HttpContext.Session.SetString("LoginOtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString());

            try
            {
                await _emailService.SendEmailAsync(
                    model.Email,
                    "Login OTP",
                    $"<p>Your login OTP is <strong>{otp}</strong>. It expires in 10 minutes.</p>"+
                    $"<p>If you did not request this code, please <a href='mailto:deepcoding15@gmail.com' style='color:#4CAF50;'>contact us</a> immediately.</p>\r\n" +
                    $"<p class='footer'>Thank you,<br/>Your Company Name</p>"
                );
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to send OTP. Please try again.");
                Console.WriteLine(ex.Message);
                return View(model);
            }

            return RedirectToAction("VerifyLoginOtp");
        }

        [HttpGet]
        public IActionResult VerifyLoginOtp()
        {
            if (HttpContext.Session.GetString("LoginOtp") == null)
            {
                TempData["Error"] = "Please log in first.";
                return RedirectToAction("Login");
            }
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> VerifyLoginOtp(ForgotPasswordViewModel model)
        {
            var storedOtp = HttpContext.Session.GetString("LoginOtp");
            var userIdStr = HttpContext.Session.GetString("LoginUserId");
            var expiryString = HttpContext.Session.GetString("LoginOtpExpiry");

            if (storedOtp == null || userIdStr == null || expiryString == null)
            {
                TempData["Error"] = "OTP expired. Try logging in again.";
                return RedirectToAction("Login");
            }

            if (DateTime.UtcNow > DateTime.Parse(expiryString))
            {
                TempData["Error"] = "OTP expired. Try logging in again.";
                return RedirectToAction("Login");
            }

            if (model.OTP != storedOtp)
            {
                TempData["Error"] = "Invalid OTP.";
                return View(model);
            }

            var user = _userRepo.GetUserById(int.Parse(userIdStr));
            if (user == null) return RedirectToAction("Login");

            // Clear OTP session
            HttpContext.Session.Remove("LoginOtp");
            HttpContext.Session.Remove("LoginUserId");
            HttpContext.Session.Remove("LoginOtpExpiry");

            // Do authentication
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
                    $"<p>Your OTP is <strong>{otp}</strong>. It expires in 10 minutes.</p>"+
                    $"<p>If you did not request this code, please <a href='mailto:deepcoding15@gmail.com' style='color:#4CAF50;'>contact us</a> immediately.</p>\r\n" +
                    $"<p class='footer'>Thank you,<br/>Your Company Name</p>"
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

        // ---------------- Update User ----------------
        [HttpGet]
        public IActionResult UpdateUser(int id)
        {
            var user = _userRepo.GetUserById(id);
            if (user == null) return NotFound();

            var model = new UpdateUserViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                // Do not populate password fields
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUser(UpdateUserViewModel model)
        {
            if (string.IsNullOrEmpty(model.NewPassword))
            {
                // Remove password validation errors if empty
                ModelState.Remove(nameof(model.NewPassword));
                ModelState.Remove(nameof(model.ConfirmPassword));
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = _userRepo.GetUserById(model.UserId);
            if (user == null) return NotFound();

            bool passwordChanged = !string.IsNullOrEmpty(model.NewPassword);

            if (passwordChanged)
            {
                // Password update requires OTP
                var salt = GenerateSalt();
                user.Password = HashPassword(model.NewPassword, salt);
                user.Salt = salt;

                var otp = GenerateOtp();
                HttpContext.Session.SetString("UpdateUserOtp", otp);
                HttpContext.Session.SetString("UpdateUserId", user.UserId.ToString());
                HttpContext.Session.SetString("UpdateOtpExpiry", DateTime.UtcNow.AddMinutes(10).ToString());
                HttpContext.Session.SetString("PendingUpdateUser",
                    System.Text.Json.JsonSerializer.Serialize(model));

                try
                {
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "Profile Update OTP",
                        $"<p>Your OTP is <strong>{otp}</strong>. It expires in 10 minutes.</p>"
                    );
                }
                catch
                {
                    return Json(new { error = "Failed to send OTP. Try again." });
                }

                return Json(new { otpRequired = true });
            }
            else
            {
                // Only username changed -> update directly
                user.Username = model.Username;
                _userRepo.Update(user);
                TempData["Message"] = "Profile updated successfully!";

                return Json(new { redirectUrl = Url.Action("Profile", new { id = user.UserId }) });
            }
        }


        [HttpGet]
        public IActionResult VerifyUpdateOtp()
        {
            if (HttpContext.Session.GetString("UpdateUserOtp") == null)
            {
                TempData["Error"] = "No update in progress.";
                return RedirectToAction("Profile");
            }
            return View(new ForgotPasswordViewModel()); // simple OTP input
        }

        [HttpPost]
        public async Task<IActionResult> VerifyUpdateOtp(ForgotPasswordViewModel model)
        {
            var storedOtp = HttpContext.Session.GetString("UpdateUserOtp");
            var expiryString = HttpContext.Session.GetString("UpdateOtpExpiry");
            var userData = HttpContext.Session.GetString("PendingUpdateUser");

            if (string.IsNullOrEmpty(storedOtp) || string.IsNullOrEmpty(expiryString) || string.IsNullOrEmpty(userData))
            {
                TempData["Error"] = "OTP session expired. Please try updating your profile again.";
                return RedirectToAction("Profile");
            }

            if (DateTime.UtcNow > DateTime.Parse(expiryString))
            {
                HttpContext.Session.Remove("UpdateUserOtp");
                HttpContext.Session.Remove("PendingUpdateUser");
                HttpContext.Session.Remove("UpdateOtpExpiry");

                TempData["Error"] = "OTP expired. Please try updating your profile again.";
                return RedirectToAction("Profile");
            }

            if (model.OTP != storedOtp)
            {
                ModelState.AddModelError("", "Invalid OTP.");
                return View(model);
            }

            var updateModel = System.Text.Json.JsonSerializer.Deserialize<UpdateUserViewModel>(userData);
            var user = _userRepo.GetUserById(updateModel.UserId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Profile");
            }

            // Apply updates
            user.Username = updateModel.Username;
            if (!string.IsNullOrEmpty(updateModel.NewPassword))
            {
                var salt = GenerateSalt();
                user.Password = HashPassword(updateModel.NewPassword, salt);
                user.Salt = salt;
            }

            _userRepo.Update(user);

            // Refresh auth if current user
            if (User.FindFirstValue(ClaimTypes.NameIdentifier) == user.UserId.ToString())
            {
                await HttpContext.SignOutAsync("MyCookieAuth");

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

                await HttpContext.SignInAsync("MyCookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
            }

            // Clear session
            HttpContext.Session.Remove("UpdateUserOtp");
            HttpContext.Session.Remove("PendingUpdateUser");
            HttpContext.Session.Remove("UpdateOtpExpiry");

            TempData["Message"] = "Profile updated successfully!";
            return RedirectToAction("Profile", new { id = user.UserId });
        }


        // ---------------- Delete User ----------------
        [HttpPost]
        public async Task<IActionResult> DeleteUser()
        {
            var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(loggedInUserId))
            {
                return RedirectToAction("Login");
            }

            var user = _userRepo.GetUserById(int.Parse(loggedInUserId));
            if (user == null) return NotFound();

            _userRepo.Delete(user.UserId);

            // Clear session and logout
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync("MyCookieAuth");

            TempData["Message"] = "Your account has been deleted successfully.";
            return RedirectToAction("Index", "Home");
        }

        //------search user profile------
        [HttpGet]
        public IActionResult Profile(int? id)
        {
            // If no ID is provided, use logged-in user's ID
            if (id == null)
            {
                var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (loggedInUserId == null)
                {
                    TempData["Error"] = "Please log in first.";
                    return RedirectToAction("Login");
                }
                id = int.Parse(loggedInUserId);
            }

            var user = _userRepo.GetUserById(id.Value);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Map to view model
            var model = new ProfileViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email
            };

            return View(model);
        }

    }
}
