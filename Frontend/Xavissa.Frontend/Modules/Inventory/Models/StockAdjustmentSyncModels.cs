using System;
using System.Collections.Generic;

namespace Xavissa.Frontend.Models;

public sealed class StockAdjustmentSyncRequestDto
{
    public Guid SyncId { get; set; }
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public int TenantId { get; set; }
    public int StoreId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<StockAdjustmentItemSyncDto> Items { get; set; } = new();
}

public sealed class StockAdjustmentItemSyncDto
{
    public Guid SyncId { get; set; }
    public int VariantId { get; set; }
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public sealed class StockAdjustmentSyncResultDto
{
    public int Id { get; set; }
    public Guid SyncId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ServerUtcNow { get; set; }
}
