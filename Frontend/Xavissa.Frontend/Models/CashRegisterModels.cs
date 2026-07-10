using System;
using System.Collections.Generic;

namespace Xavissa.Frontend.Models
{
    public class CashRegisterSession
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public int OpenedByUserId { get; set; }
        public int? ClosedByUserId { get; set; }
        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }
        public decimal OpeningCashAmount { get; set; }
        public decimal? ExpectedCashAmount { get; set; }
        public decimal? CountedCashAmount { get; set; }
        public decimal? DifferenceAmount { get; set; }
        public string Status { get; set; } = "Open";
        public string? Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public List<CashRegisterCashMovement> CashMovements { get; set; } = new();
    }

    public class CashRegisterCashMovement
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public int CashRegisterSessionId { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedBy { get; set; }
    }
}
