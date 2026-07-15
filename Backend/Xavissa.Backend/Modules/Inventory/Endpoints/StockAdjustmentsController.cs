using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Route("api/v1/stock-adjustments")]
public class StockAdjustmentsController : ControllerBase
{
    private readonly StockAdjustmentService _service;

    public StockAdjustmentsController(StockAdjustmentService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<StockAdjustmentReadDto>> Create(StockAdjustmentCreateDto request) =>
        await Handle(async () => Ok(Map(await _service.CreateAsync(request))));

    [HttpPost("sync-apply")]
    public async Task<ActionResult<StockAdjustmentSyncResultDto>> SyncApply(
        StockAdjustmentSyncRequestDto request)
    {
        try
        {
            var adjustment = await _service.ApplySyncedAsync(request);
            return Ok(new StockAdjustmentSyncResultDto
            {
                Id = adjustment.Id,
                SyncId = adjustment.SyncId,
                Status = adjustment.Status,
                ServerUtcNow = DateTime.UtcNow,
            });
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

    [HttpGet]
    public async Task<ActionResult<List<StockAdjustmentReadDto>>> List() =>
        Ok((await _service.ListAsync()).Select(Map).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StockAdjustmentReadDto>> Get(int id)
    {
        var adjustment = await _service.GetAsync(id);
        return adjustment == null ? NotFound() : Ok(Map(adjustment));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<StockAdjustmentReadDto>> Approve(int id) => await Handle(async () => Ok(Map(await _service.ApproveAsync(id))));

    [HttpPost("{id:int}/apply")]
    public async Task<ActionResult<StockAdjustmentReadDto>> Apply(int id) => await Handle(async () => Ok(Map(await _service.ApplyAsync(id))));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<StockAdjustmentReadDto>> Cancel(int id) => await Handle(async () => Ok(Map(await _service.CancelAsync(id))));

    private async Task<ActionResult<StockAdjustmentReadDto>> Handle(Func<Task<ActionResult<StockAdjustmentReadDto>>> action)
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

    private static StockAdjustmentReadDto Map(StockAdjustment adjustment) => new()
    {
        Id = adjustment.Id,
        SyncId = adjustment.SyncId,
        TenantId = adjustment.TenantId,
        StoreId = adjustment.StoreId,
        AdjustmentNumber = adjustment.AdjustmentNumber,
        Reason = adjustment.Reason,
        Status = adjustment.Status,
        ApprovedAt = adjustment.ApprovedAt,
        AppliedAt = adjustment.AppliedAt,
        CancelledAt = adjustment.CancelledAt,
        Notes = adjustment.Notes,
        Items = adjustment.Items.Select(item => new StockAdjustmentItemCreateDto
        {
            VariantId = item.VariantId,
            NewQuantity = item.NewQuantity,
            Reason = item.Reason,
            Notes = item.Notes,
        }).ToList(),
    };
}
