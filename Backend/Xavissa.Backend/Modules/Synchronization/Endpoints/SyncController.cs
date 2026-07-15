using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/sync")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly SyncService _syncService;

    public SyncController(SyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpGet("bootstrap")]
    public async Task<ActionResult<StoreBootstrapSyncDto>> GetBootstrap([FromQuery] bool includeCatalog = false)
    {
        try
        {
            return Ok(await _syncService.GetBootstrapAsync(includeCatalog));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpGet("sellable-variants")]
    public async Task<ActionResult<StoreSellableVariantsDeltaDto>> GetSellableVariants(
        [FromQuery] int storeId,
        [FromQuery] DateTime? updatedAfter = null)
    {
        try
        {
            return Ok(await _syncService.GetSellableVariantsDeltaAsync(storeId, updatedAfter));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpGet("stock-levels")]
    public async Task<ActionResult<StockLevelsDeltaDto>> GetStockLevels(
        [FromQuery] int storeId,
        [FromQuery] DateTime? updatedAfter = null)
    {
        try
        {
            return Ok(await _syncService.GetStockLevelsDeltaAsync(storeId, updatedAfter));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpPost("stock-check")]
    public async Task<ActionResult<LiveStockCheckResponseDto>> CheckLiveStock(
        [FromBody] LiveStockCheckRequestDto request)
    {
        try
        {
            return Ok(await _syncService.GetLiveStockAsync(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<CatalogDeltaDto>> GetCatalog(
        [FromQuery] int tenantId,
        [FromQuery] DateTime? updatedAfter = null)
    {
        try
        {
            return Ok(await _syncService.GetCatalogDeltaAsync(tenantId, updatedAfter));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpGet("sales")]
    public async Task<ActionResult<SalesDeltaDto>> GetSales(
        [FromQuery] int storeId,
        [FromQuery] DateTime? updatedAfter = null)
    {
        try
        {
            return Ok(await _syncService.GetSalesDeltaAsync(storeId, updatedAfter));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpPost("sales/upload")]
    public async Task<ActionResult<SalesUploadBatchResultDto>> UploadSales(
        [FromBody] SalesUploadBatchRequestDto request)
    {
        return Ok(await _syncService.UploadSalesBatchAsync(request));
    }
}
