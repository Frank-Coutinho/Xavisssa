using System.Threading.Tasks;

namespace Xavissa.Frontend.Services
{
    public interface ISyncService
    {
        Task SyncAllAsync(bool replaceStoreScopedProductCache = false);
        Task SyncStoreScopedDataAsync(bool replaceStoreScopedProductCache = false);
        Task SyncUsersAsync();
        Task SyncProductsAsync();
        Task SyncSalesAsync();
        Task RefreshOperationalDataAsync();
        Task BootstrapAsync(bool includeCatalog);
        Task SyncAfterLoginAsync();
        Task SyncAfterReconnectAsync();
        Task SyncStoreScopedDataAsync(int storeId);
        Task SyncAfterSaleAsync();
        Task SyncAfterSaleAsync(int storeId);
        Task UploadPendingSalesAsync();
        Task PullCatalogDeltaAsync(int tenantId);
        Task PullSellableVariantsDeltaAsync(int storeId);
        Task PullStockLevelsDeltaAsync(int storeId);
        Task PullSalesDeltaAsync(int storeId);
    }
}
