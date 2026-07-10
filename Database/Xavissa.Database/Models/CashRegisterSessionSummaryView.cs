namespace Xavissa.Database.Models;

public class CashRegisterSessionSummaryView
{
    public int? CashRegisterSessionId { get; set; }
    public int? TenantId { get; set; }
    public int? StoreId { get; set; }
    public int? OpenedByUserId { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? OpeningCashAmount { get; set; }
    public string? Status { get; set; }
    public decimal? CashPaymentsTotal { get; set; }
    public decimal? NonCashPaymentsTotal { get; set; }
    public decimal? AllPaymentsTotal { get; set; }
    public decimal? CashInTotal { get; set; }
    public decimal? CashOutTotal { get; set; }
    public decimal? CalculatedExpectedCashAmount { get; set; }
}
