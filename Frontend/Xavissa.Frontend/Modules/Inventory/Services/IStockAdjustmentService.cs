using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface IStockAdjustmentService
{
    Task<StockAdjustment> ApplyLocalAsync(
        int tenantId,
        int storeId,
        string reason,
        IEnumerable<LocalStockAdjustmentLine> items);
}

public interface IStockAdjustmentSyncService
{
    Task SyncPendingAsync();
}

public sealed record LocalStockAdjustmentLine(
    int VariantId,
    int NewQuantity,
    string? Reason = null,
    string? Notes = null);
