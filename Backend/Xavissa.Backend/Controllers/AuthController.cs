using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;
using Microsoft.AspNetCore.Authorization;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        // Public registration (e.g., for first Superuser setup)
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var user = await _authService.Register(
                request.Username,
                request.Email,
                request.Password,
                request.Role ?? UserRole.Clerk // Default Clerk if not specified
            );

            if (user == null) 
                return BadRequest("User already exists");

            return Ok(new { user.Id, user.Username, user.Role });
        }

        // Login route (public)
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var token = await _authService.Login(request.Username, request.Password);
            if (token == null) 
                return Unauthorized("Invalid credentials");

            return Ok(new { Token = token });
        }
    }

    // DTOs
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserRole? Role { get; set; } // Optional role field
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
