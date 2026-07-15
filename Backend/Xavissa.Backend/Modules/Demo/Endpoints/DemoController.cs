using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private const string DemoDisabledMessage = "Demo mode is currently disabled.";
    private readonly IDemoService _demo;
    private readonly XavissaDbContext _db;
    private readonly IConfiguration _configuration;

    public DemoController(IDemoService demo, XavissaDbContext db, IConfiguration configuration)
    {
        _demo = demo;
        _db = db;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] DemoStartRequest request)
    {
        if (!DemosEnabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, DemoDisabledMessage);

        try
        {
            return Ok(await _demo.StartDemoAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpPost("events")]
    public async Task<IActionResult> TrackEvent([FromBody] DemoSessionEventRequest request)
    {
        if (!DemosEnabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, DemoDisabledMessage);

        return await _demo.TrackEventAsync(request) ? Ok() : NotFound();
    }

    [AllowAnonymous]
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateDemoSessionRequest request)
    {
        if (!DemosEnabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, DemoDisabledMessage);

        var result = await _demo.ValidateDemoAsync(request);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize(Roles = AccessRoles.SystemAdmin)]
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions()
    {
        return Ok(await _db.DemoSessions.IgnoreQueryFilters().OrderByDescending(x => x.StartedAt).ToListAsync());
    }

    [Authorize(Roles = AccessRoles.SystemAdmin)]
    [HttpGet("sessions/{id:int}/events")]
    public async Task<IActionResult> Events(int id)
    {
        return Ok(await _db.DemoSessionEvents.Where(x => x.DemoSessionId == id).OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    [Authorize(Roles = AccessRoles.SystemAdmin)]
    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] DemoTemplate template)
    {
        _db.DemoTemplates.Add(template);
        await _db.SaveChangesAsync();
        return Ok(template);
    }

    private bool DemosEnabled => _configuration.GetValue<bool>("Features:EnableDemos");
}
