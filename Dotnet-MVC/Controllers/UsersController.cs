using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DotnetMVCApp.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepo _userRepo;
        private readonly IConfiguration _config;

        public UsersController(IUserRepo userRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _config = config;
        }

        // Generate Salt
        private string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        // Hash Password + Salt
        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var combined = Encoding.UTF8.GetBytes(password + salt);
                var hashBytes = sha256.ComputeHash(combined);
                return Convert.ToBase64String(hashBytes);
            }
        }

        // ---------------- SIGN UP ----------------
        [HttpPost("signup")]
        public IActionResult SignUp([FromBody] User user)
        {
            if (_userRepo.GetUserByEmail(user.Email) != null)
                return BadRequest("Email already registered.");

            string salt = GenerateSalt();
            string hashedPassword = HashPassword(user.Password, salt);

            user.Password = hashedPassword;
            user.Salt = salt;

            var createdUser = _userRepo.Add(user);

            return Ok(new
            {
                Message = "User registered successfully",
                UserId = createdUser.UserId,
                Email = createdUser.Email
            });
        }

        // ---------------- LOGIN ----------------
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginModel loginModel)
        {
            var existingUser = _userRepo.GetUserByEmail(loginModel.Email);
            if (existingUser == null)
                return Unauthorized("Invalid email or password.");

            string hashedInput = HashPassword(loginModel.Password, existingUser.Salt);
            if (existingUser.Password != hashedInput)
                return Unauthorized("Invalid email or password.");

            // Create JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, existingUser.UserId.ToString()),
                    new Claim(ClaimTypes.Email, existingUser.Email),
                    // Add roles/claims here if needed
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            string jwtToken = tokenHandler.WriteToken(token);

            return Ok(new
            {
                Message = "Login successful",
                Token = jwtToken,
                UserId = existingUser.UserId,
                Email = existingUser.Email
            });
        }

        // ---------------- SECURED ENDPOINT ----------------
        [Authorize]
        [HttpGet("all")]
        public IActionResult GetAllUsers()
        {
            var users = _userRepo.GetAllUser();
            return Ok(users);
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult GetMyProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
                return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var user = _userRepo.GetUserById(userId);

            return Ok(new
            {
                UserId = user.UserId,
                Email = user.Email
            });
        }
    }

    // DTO for Login
    public class LoginModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
