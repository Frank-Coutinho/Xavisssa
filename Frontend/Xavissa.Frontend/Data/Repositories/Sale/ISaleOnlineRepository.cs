using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface ISaleOnlineRepository
    {
        Task<List<Sale>> GetAllAsync();
        Task<Sale> CreateAsync(Sale sale);
        Task SoftDeleteSaleAsync(int saleId, string reason);
        Task RefundSaleAsync(int saleId, string reason);
        Task RefundSaleItemAsync(int saleId, int saleItemId, int quantity, string reason);
        // Task DebugPrintServerSales();
    }
}
