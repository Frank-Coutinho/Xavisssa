using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Route("api/v1/cash-register")]
public class CashRegisterController : ControllerBase
{
    private readonly CashRegisterService _cashRegisterService;

    public CashRegisterController(CashRegisterService cashRegisterService)
    {
        _cashRegisterService = cashRegisterService;
    }

    [HttpPost("open")]
    public async Task<ActionResult<CashRegisterSessionDto>> Open(CashRegisterOpenRequestDto request) =>
        await HandleSession(async () => Ok(Map(await _cashRegisterService.OpenAsync(request))));

    [HttpPost("close")]
    public async Task<ActionResult<CashRegisterSessionDto>> Close(CashRegisterCloseRequestDto request) =>
        await HandleSession(async () => Ok(Map(await _cashRegisterService.CloseAsync(request))));

    [HttpGet("current")]
    public async Task<ActionResult<CashRegisterSessionDto>> Current([FromQuery] int? storeId, [FromQuery] string? sourceDeviceId)
    {
        try
        {
            var session = await _cashRegisterService.GetCurrentAsync(storeId, sourceDeviceId);
            return session == null ? NotFound() : Ok(Map(session));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("cash-movements")]
    public async Task<ActionResult<object>> CashMovement(CashRegisterMovementRequestDto request) =>
        await HandleObject(async () =>
        {
            var movement = await _cashRegisterService.AddMovementAsync(request);
            return Ok(new { movement.Id, movement.CashRegisterSessionId, movement.MovementType, movement.Amount });
        });

    [HttpGet("sessions/{id:int}/summary")]
    public async Task<ActionResult<CashRegisterSummaryDto>> Summary(int id) =>
        await HandleSummary(async () => Ok(await _cashRegisterService.GetSummaryAsync(id)));

    private async Task<ActionResult<CashRegisterSessionDto>> HandleSession(Func<Task<ActionResult<CashRegisterSessionDto>>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<ActionResult<CashRegisterSummaryDto>> HandleSummary(Func<Task<ActionResult<CashRegisterSummaryDto>>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<ActionResult<object>> HandleObject(Func<Task<ActionResult<object>>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    private static CashRegisterSessionDto Map(CashRegisterSession session) => new()
    {
        Id = session.Id,
        SyncId = session.SyncId,
        TenantId = session.TenantId,
        StoreId = session.StoreId,
        OpenedByUserId = session.OpenedByUserId,
        ClosedByUserId = session.ClosedByUserId,
        SourceDeviceId = session.SourceDeviceId,
        OpenedAt = session.OpenedAt,
        ClosedAt = session.ClosedAt,
        OpeningCashAmount = session.OpeningCashAmount,
        ExpectedCashAmount = session.ExpectedCashAmount,
        CountedCashAmount = session.CountedCashAmount,
        DifferenceAmount = session.DifferenceAmount,
        Status = session.Status,
        Notes = session.Notes,
    };
}
