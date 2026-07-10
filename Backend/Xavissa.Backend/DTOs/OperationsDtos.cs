namespace Xavissa.Backend.DTOs;

public class StoreOperationalSettingsDto
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public string CashRegisterMode { get; set; } = "Disabled";
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateStoreOperationalSettingsDto
{
    public string CashRegisterMode { get; set; } = "Disabled";
}

public class CashRegisterOpenRequestDto
{
    public int? StoreId { get; set; }
    public string? SourceDeviceId { get; set; }
    public decimal OpeningCashAmount { get; set; }
    public string? Notes { get; set; }
}

public class CashRegisterCloseRequestDto
{
    public int? SessionId { get; set; }
    public int? StoreId { get; set; }
    public string? SourceDeviceId { get; set; }
    public decimal? CountedCashAmount { get; set; }
    public string? Notes { get; set; }
}

public class CashRegisterMovementRequestDto
{
    public int? SessionId { get; set; }
    public int? StoreId { get; set; }
    public string? SourceDeviceId { get; set; }
    public string MovementType { get; set; } = "CashIn";
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}

public class CashRegisterSessionDto
{
    public int Id { get; set; }
    public Guid SyncId { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public int OpenedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public string? SourceDeviceId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningCashAmount { get; set; }
    public decimal? ExpectedCashAmount { get; set; }
    public decimal? CountedCashAmount { get; set; }
    public decimal? DifferenceAmount { get; set; }
    public string Status { get; set; } = "Open";
    public string? Notes { get; set; }
}

public class CashRegisterSummaryDto : CashRegisterSessionDto
{
    public decimal CashSalesTotal { get; set; }
    public decimal CashInTotal { get; set; }
    public decimal CashOutTotal { get; set; }
}

public class StockTransferCreateDto
{
    public int FromStoreId { get; set; }
    public int ToStoreId { get; set; }
    public string? Notes { get; set; }
    public List<StockTransferItemCreateDto> Items { get; set; } = new();
}

public class StockTransferItemCreateDto
{
    public int VariantId { get; set; }
    public int QuantityRequested { get; set; }
    public int? QuantityApproved { get; set; }
    public string? Notes { get; set; }
}

public class StockTransferReadDto : StockTransferCreateDto
{
    public int Id { get; set; }
    public Guid SyncId { get; set; }
    public int? TenantId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class StockAdjustmentCreateDto
{
    public int StoreId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<StockAdjustmentItemCreateDto> Items { get; set; } = new();
}

public class StockAdjustmentItemCreateDto
{
    public int VariantId { get; set; }
    public int NewQuantity { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public class StockAdjustmentReadDto : StockAdjustmentCreateDto
{
    public int Id { get; set; }
    public Guid SyncId { get; set; }
    public int? TenantId { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime? ApprovedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class SyncConflictDto
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public int? StoreId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid? EntitySyncId { get; set; }
    public string ConflictType { get; set; } = string.Empty;
    public string? LocalPayloadJson { get; set; }
    public string? ServerPayloadJson { get; set; }
    public string ResolutionStatus { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }
}

public class SyncConflictResolutionRequestDto
{
    public string? Notes { get; set; }
}
