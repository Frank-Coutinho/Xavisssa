using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roles;

    public RolesController(IRoleService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> GetRoles([FromQuery] string? scope = null)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return Ok(await _roles.GetRolesAsync());

        return Ok(await _roles.GetRolesByScopeAsync(scope.Trim()));
    }
}
