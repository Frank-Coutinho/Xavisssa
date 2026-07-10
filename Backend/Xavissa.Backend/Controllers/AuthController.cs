using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/v1/auth")]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            return BadRequest(
                "Use a role-specific endpoint under /api/UserManagement: create-system-admin, create-support, create-tenant-admin, create-store-manager, or create-clerk."
            );
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request.Username, request.Password);
            return response == null ? Unauthorized("Invalid credentials or no active assignment.") : Ok(response);
        }

        [AllowAnonymous]
        [HttpPost("select-store")]
        public async Task<IActionResult> SelectStore([FromBody] SelectStoreRequest request)
        {
            var response = await _authService.SelectStoreAsync(request.Username, request.Password, request.StoreId);
            return response == null ? Unauthorized("Invalid credentials or store not assigned.") : Ok(response);
        }
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PlatformRole { get; set; }
        public string? AssignedRole { get; set; }
        public int? TenantId { get; set; }
        public int? StoreId { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SelectStoreRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int StoreId { get; set; }
    }
}
