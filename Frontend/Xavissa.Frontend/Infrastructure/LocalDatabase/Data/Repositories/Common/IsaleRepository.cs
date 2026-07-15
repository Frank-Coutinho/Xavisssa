using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface ISaleRepository
    {
        event Action? SalesChanged;
        Task<List<Sale>> GetAllAsync();
        Task<List<Sale>> GetHistoryPageAsync(SaleHistoryQuery query);
        Task<Sale> CreateAsync(Sale sale);
        Task SoftDeleteSaleAsync(int saleId, string reason);
        Task RefundSaleAsync(int saleId, string reason);
        Task RefundSaleItemAsync(int saleId, int saleItemId, int quantity, string reason);
        Task SyncAsync();
    }
}
