using DotnetMVCApp.DTOs;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using HiringAssistance.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotnetMVCApp.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepo _userRepo;

        public UsersController(IUserRepo userRepo)
        {
            _userRepo = userRepo;
        }

        [HttpPost("clerk-login")]
        public IActionResult ClerkLogin([FromBody] ClerkUserDto clerkUser)
        {
            try
            {
                // Validate input
                if (clerkUser == null
                    || string.IsNullOrEmpty(clerkUser.ClerkId)
                    || string.IsNullOrEmpty(clerkUser.Email))
                {
                    return BadRequest("ClerkId and Email are required.");
                }

                // Check if user already exists
                var existingUser = _userRepo.GetAllUser()
                    .FirstOrDefault(u => u.ClerkId == clerkUser.ClerkId);

                if (existingUser != null)
                {
                    return Ok(existingUser);
                }

                Console.WriteLine("Received role: " + clerkUser.Role);


                // Create new user
                var newUser = new User
                {
                    ClerkId = clerkUser.ClerkId,
                    Email = clerkUser.Email,

                    Role = Enum.TryParse<UserRole>(clerkUser.Role, true, out var role) ? role : UserRole.Candidate
                };

                var addedUser = _userRepo.Add(newUser);

                return Ok(addedUser);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClerkLogin error: " + ex.Message);
                return StatusCode(500, ex.Message);
            }
        }
    }
}
