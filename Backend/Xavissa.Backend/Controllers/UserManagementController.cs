using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;
using Xavissa.Backend.Utilities;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Only logged-in users
    public class UserManagementController : ControllerBase
    {
        private readonly AuthService _authService;

        public UserManagementController(AuthService authService)
        {
            _authService = authService;
        }

        // ✅ Superuser creates Admins
        [HttpPost("create-admin")]
        [Authorize(Roles = "Superuser")]
        public async Task<IActionResult> CreateAdmin([FromBody] RegisterRequest request)
        {
            var user = await _authService.Register(
                request.Username,
                request.Email,
                request.Password,
                UserRole.Admin
            );
            if (user == null)
                return BadRequest("User already exists");
            return Ok(user);
        }

        // ✅ Admin creates Clerks
        [HttpPost("create-clerk")]
        [Authorize(Roles = "Superuser,Admin")]
        public async Task<IActionResult> CreateClerk([FromBody] RegisterRequest request)
        {
            var user = await _authService.Register(
                request.Username,
                request.Email,
                request.Password,
                UserRole.Clerk
            );
            if (user == null)
                return BadRequest("User already exists");
            return Ok(user);
        }

        // ✅ Update a user (Admin or Superuser only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Superuser,Admin")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var currentRole = User.FindFirst("role")?.Value;
            var result = await _authService.UpdateUserAsync(id, request, currentRole);
            if (!result)
                return NotFound("User not found or not allowed.");
            return Ok("User updated successfully");
        }

        // ✅ Delete a user (Superuser or Admin)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Superuser,Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentRole = User.FindFirst("role")?.Value;
            var result = await _authService.DeleteUserAsync(id, currentRole);
            if (!result)
                return NotFound("User not found or not allowed.");
            return Ok("User deleted successfully");
        }
    }

    public class UpdateUserAdminRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }
}
