using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IAnalyticsRepository
    {
        Task<TenantAnalyticsSummary> GetTenantAnalyticsAsync(int tenantId);
        Task<StoreAnalyticsResponse> GetStoreAnalyticsAsync();
    }
}
