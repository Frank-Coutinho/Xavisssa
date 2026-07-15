using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/Sync/conflicts")]
[Route("api/v1/sync/conflicts")]
public class SyncConflictsController : ControllerBase
{
    private readonly SyncConflictService _service;

    public SyncConflictsController(SyncConflictService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<SyncConflictDto>>> List() =>
        Ok((await _service.ListAsync()).Select(Map).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SyncConflictDto>> Get(int id)
    {
        var conflict = await _service.GetAsync(id);
        return conflict == null ? NotFound() : Ok(Map(conflict));
    }

    [HttpPost("{id:int}/resolve")]
    public async Task<ActionResult<SyncConflictDto>> Resolve(int id, SyncConflictResolutionRequestDto request) =>
        await Handle(async () => Ok(Map(await _service.ResolveAsync(id, request.Notes))));

    [HttpPost("{id:int}/ignore")]
    public async Task<ActionResult<SyncConflictDto>> Ignore(int id, SyncConflictResolutionRequestDto request) =>
        await Handle(async () => Ok(Map(await _service.IgnoreAsync(id, request.Notes))));

    private async Task<ActionResult<SyncConflictDto>> Handle(Func<Task<ActionResult<SyncConflictDto>>> action)
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
    }

    private static SyncConflictDto Map(SyncConflict conflict) => new()
    {
        Id = conflict.Id,
        TenantId = conflict.TenantId,
        StoreId = conflict.StoreId,
        EntityName = conflict.EntityName,
        EntitySyncId = conflict.EntitySyncId,
        ConflictType = conflict.ConflictType,
        LocalPayloadJson = conflict.LocalPayloadJson,
        ServerPayloadJson = conflict.ServerPayloadJson,
        ResolutionStatus = conflict.ResolutionStatus,
        ResolutionNotes = conflict.ResolutionNotes,
        CreatedAt = conflict.CreatedAt,
        ResolvedAt = conflict.ResolvedAt,
        ResolvedByUserId = conflict.ResolvedByUserId,
    };
}
