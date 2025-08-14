using DotnetMVCApp.Data; // Your DbContext namespace
using DotnetMVCApp.Models; // Your AppUser model namespace
using HiringAssistance.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DotnetMVCApp.Controllers
{
    [Route("Auth")]
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("SyncUser")]
        public async Task<IActionResult> SyncUser([FromBody] SyncUserRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Role))
                return BadRequest(new { message = "Email and role are required." });

            // Check if user already exists
            var existingUser = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUser != null)
            {
                // Optionally update role if it changed
                existingUser.Role = Enum.Parse<UserRole>(request.Role);
                _context.AppUsers.Update(existingUser);
            }
            else
            {
                // Create new user
                var newUser = new User
                {
                    Email = request.Email,
                    Role = Enum.Parse<UserRole>(request.Role)
                    // Fill other optional fields if needed
                };
                _context.AppUsers.Add(newUser);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "User synced successfully." });
        }
    }

    // DTO for incoming request
    public class SyncUserRequest
    {
        public string Email { get; set; }
        public string Role { get; set; }
    }
}
