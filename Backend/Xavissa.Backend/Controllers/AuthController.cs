using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;

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

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var user = await _authService.Register(request.Username, request.Email, request.Password);
            if (user == null) return BadRequest("User already exists");
            return Ok(user);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var token = await _authService.Login(request.Username, request.Password);
            if (token == null) return Unauthorized();
            return Ok(new { Token = token });
        }
    }

    // Request DTOs
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
