using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Route("api/v1/stock-transfers")]
public class StockTransfersController : ControllerBase
{
    private readonly StockTransferService _service;

    public StockTransfersController(StockTransferService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<StockTransferReadDto>> Create(StockTransferCreateDto request) =>
        await Handle(async () => Ok(Map(await _service.CreateAsync(request))));

    [HttpGet]
    public async Task<ActionResult<List<StockTransferReadDto>>> List() =>
        Ok((await _service.ListAsync()).Select(Map).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StockTransferReadDto>> Get(int id)
    {
        var transfer = await _service.GetAsync(id);
        return transfer == null ? NotFound() : Ok(Map(transfer));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<StockTransferReadDto>> Approve(int id) => await Handle(async () => Ok(Map(await _service.ApproveAsync(id))));

    [HttpPost("{id:int}/ship")]
    public async Task<ActionResult<StockTransferReadDto>> Ship(int id) => await Handle(async () => Ok(Map(await _service.ShipAsync(id))));

    [HttpPost("{id:int}/receive")]
    public async Task<ActionResult<StockTransferReadDto>> Receive(int id) => await Handle(async () => Ok(Map(await _service.ReceiveAsync(id))));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<StockTransferReadDto>> Cancel(int id) => await Handle(async () => Ok(Map(await _service.CancelAsync(id))));

    private async Task<ActionResult<StockTransferReadDto>> Handle(Func<Task<ActionResult<StockTransferReadDto>>> action)
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

    private static StockTransferReadDto Map(StockTransfer transfer) => new()
    {
        Id = transfer.Id,
        SyncId = transfer.SyncId,
        TenantId = transfer.TenantId,
        TransferNumber = transfer.TransferNumber,
        FromStoreId = transfer.FromStoreId,
        ToStoreId = transfer.ToStoreId,
        Status = transfer.Status,
        RequestedAt = transfer.RequestedAt,
        ApprovedAt = transfer.ApprovedAt,
        SentAt = transfer.SentAt,
        ReceivedAt = transfer.ReceivedAt,
        CancelledAt = transfer.CancelledAt,
        Notes = transfer.Notes,
        Items = transfer.Items.Select(item => new StockTransferItemCreateDto
        {
            VariantId = item.VariantId,
            QuantityRequested = item.QuantityRequested,
            QuantityApproved = item.QuantityApproved,
            Notes = item.Notes,
        }).ToList(),
    };
}
