using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // All endpoints require authentication
    public class UsersController : ControllerBase
    {
        private readonly AuthService _authService;

        public UsersController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            return Ok(
                new
                {
                    Username = User.Identity?.Name,
                    Role = User.FindFirst("role")?.Value, // custom claim
                    ClaimTypesRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, // default claim type
                    AllClaims = User.Claims.Select(c => new { c.Type, c.Value }),
                }
            );
        }

        // ✅ Superuser only: list all users
        [HttpGet("all")]
        [Authorize(Roles = "Superuser")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _authService.GetAllUsersAsync();
            return Ok(users);
        }

        // ✅ Admin and Superuser: get a specific user
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Superuser")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _authService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound();
            return Ok(user);
        }

        // ✅ Admin and Superuser: update user info
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Superuser")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var currentRole = User.FindFirst("role")?.Value;
            var success = await _authService.UpdateUserAsync(id, request, currentRole);

            if (!success)
                return Forbid();
            return Ok("User updated successfully");
        }

        // ✅ Admin and Superuser: delete users
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Superuser")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentRole = User.FindFirst("role")?.Value;
            var success = await _authService.DeleteUserAsync(id, currentRole);

            if (!success)
                return Forbid();
            return Ok("User deleted successfully");
        }
    }

    // Request DTO for updates
    public class UpdateUserSelfRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public UserRole? Role { get; set; }
    }
}
