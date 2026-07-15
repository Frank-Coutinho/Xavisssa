using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface ISaleOfflineRepository
    {
        Task<List<Sale>> GetAllAsync();
        Task<List<Sale>> GetHistoryPageAsync(SaleHistoryQuery query);
        Task<List<Sale>> GetUnsyncedAsync();
        Task AddAsync(Sale sale);
        Task MarkAsSyncedAsync(int id);
        Task MarkAsSyncedAsync(int id, int? onlineId, Guid? syncId);
        Task MarkAsFailedAsync(int saleId, int? conflictId = null, string? error = null);
        Task UpdateLocalSaleIdAsync(int oldId, int newId);
        Task UpsertRangeAsync(IEnumerable<Sale> sales);
        Task DeleteRangeAsync(IEnumerable<int> saleIds);
        Task<DateTime?> GetCursorAsync(string key);
        Task SetCursorAsync(string key, DateTime? value);
    }
}
