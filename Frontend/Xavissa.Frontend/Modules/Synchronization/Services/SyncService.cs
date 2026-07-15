using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services
{
    public class SyncService : ISyncService
    {
        private readonly IConnectivityService _net;
        private readonly IAuthService _auth;
        private readonly IUserRepository _users;
        private readonly IProductRepositoryOffline _productOffline;
        private readonly ISaleOfflineRepository _salesOffline;
        private readonly IProductRepository _products;
        private readonly ISaleRepository _sales;
        private readonly IStoreAdminRepository _stores;
        private readonly IApiTokenProvider _tokens;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILicenseStateService _licenseState;
        private readonly ILicenseFeatureGate _featureGate;
        private readonly IDemoStateService _demoState;
        private readonly IStockAdjustmentSyncService _stockAdjustments;

        public SyncService(
            IConnectivityService net,
            IAuthService auth,
            IUserRepository users,
            IProductRepositoryOffline productOffline,
            ISaleOfflineRepository salesOffline,
            IProductRepository products,
            ISaleRepository sales,
            IStoreAdminRepository stores,
            IApiTokenProvider tokens,
            IHttpClientFactory httpFactory,
            ILicenseStateService licenseState,
            ILicenseFeatureGate featureGate,
            IDemoStateService demoState,
            IStockAdjustmentSyncService stockAdjustments
        )
        {
            _net = net;
            _auth = auth;
            _users = users;
            _productOffline = productOffline;
            _salesOffline = salesOffline;
            _products = products;
            _sales = sales;
            _stores = stores;
            _tokens = tokens;
            _httpFactory = httpFactory;
            _licenseState = licenseState;
            _featureGate = featureGate;
            _demoState = demoState;
            _stockAdjustments = stockAdjustments;
        }

        private bool CanUseAuthenticatedOnline() =>
            _net.IsOnline() && !string.IsNullOrWhiteSpace(_tokens.Token);

        private HttpClient Client => _httpFactory.CreateClient("backend");

        public async Task SyncAllAsync(bool replaceStoreScopedProductCache = false)
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                return;

            await UploadPendingSalesAsync();
            await _stockAdjustments.SyncPendingAsync();
            await _users.SyncFromServerAsync();
            await _stores.GetStoresAsync();
            await ApplyBootstrapAsync(includeCatalog: ShouldIncludeCatalogBootstrap());
            await _sales.SyncAsync();
        }

        public async Task SyncStoreScopedDataAsync(bool replaceStoreScopedProductCache = false)
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                return;

            if (_auth.SelectedStoreId.HasValue)
            {
                await ApplyBootstrapAsync(includeCatalog: _auth.IsStoreManager || _auth.IsTenantAdmin);
                await _sales.SyncAsync();
                return;
            }

            if (_auth.SelectedTenantId.HasValue && _auth.IsTenantAdmin)
                await SyncCatalogDeltaAsync(_auth.SelectedTenantId.Value);
        }

        public async Task SyncUsersAsync()
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await _users.SyncFromServerAsync();
        }

        public async Task SyncProductsAsync()
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                return;

            if (_auth.SelectedStoreId.HasValue)
            {
                await SyncSellableVariantsDeltaAsync(_auth.SelectedStoreId.Value);
                await SyncStockDeltaAsync(_auth.SelectedStoreId.Value);
            }

            if (_auth.SelectedTenantId.HasValue && (_auth.IsTenantAdmin || _auth.IsStoreManager))
                await SyncCatalogDeltaAsync(_auth.SelectedTenantId.Value);
        }

        public async Task SyncSalesAsync()
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await _sales.SyncAsync();
        }

        public async Task RefreshOperationalDataAsync()
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation") || !_auth.SelectedStoreId.HasValue)
                return;

            await _stockAdjustments.SyncPendingAsync();
            await _sales.SyncAsync();
            await SyncSellableVariantsDeltaAsync(_auth.SelectedStoreId.Value);
            await SyncStockDeltaAsync(_auth.SelectedStoreId.Value);
        }

        public async Task SyncAfterReconnectAsync()
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                return;

            await UploadPendingSalesAsync();
            await _stockAdjustments.SyncPendingAsync();
            await SyncStoreScopedDataAsync();
        }

        public async Task SyncAfterSaleAsync()
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation") || !_auth.SelectedStoreId.HasValue)
                return;

            await _sales.SyncAsync();
            await SyncStockDeltaAsync(_auth.SelectedStoreId.Value);
        }

        public async Task BootstrapAsync(bool includeCatalog)
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await ApplyBootstrapAsync(includeCatalog);
        }

        public Task SyncAfterLoginAsync() => SyncAllAsync();

        public async Task SyncStoreScopedDataAsync(int storeId)
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                return;

            await SyncSellableVariantsDeltaAsync(storeId);
            await SyncStockDeltaAsync(storeId);
            await PullSalesDeltaAsync(storeId);
        }

        public Task SyncAfterSaleAsync(int storeId) => SyncAfterSaleAsync();

        public async Task SyncAfterStockAdjustmentAsync()
        {
            if (!await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation") || !_auth.SelectedStoreId.HasValue)
                return;

            await _stockAdjustments.SyncPendingAsync();
            await SyncStockDeltaAsync(_auth.SelectedStoreId.Value);
        }

        public async Task UploadPendingSalesAsync()
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await _sales.SyncAsync();
        }

        public async Task PullCatalogDeltaAsync(int tenantId)
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await SyncCatalogDeltaAsync(tenantId);
        }

        public async Task PullSellableVariantsDeltaAsync(int storeId)
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await SyncSellableVariantsDeltaAsync(storeId);
        }

        public async Task PullStockLevelsDeltaAsync(int storeId)
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await SyncStockDeltaAsync(storeId);
        }

        public async Task PullSalesDeltaAsync(int storeId)
        {
            if (await CanUseAuthenticatedOnlineWithLicenseAsync("SyncValidation"))
                await _sales.SyncAsync();
        }

        private async Task<bool> CanUseAuthenticatedOnlineWithLicenseAsync(string validationType)
        {
            if (_demoState.IsDemoActive)
            {
                await _demoState.TrackEventAsync("CloudSyncBlocked", "Sync", validationType, "Cloud sync is disabled in Demo Mode.");
                return false;
            }

            if (!CanUseAuthenticatedOnline())
                return false;

            await Task.CompletedTask;
            return true;
        }

        private async Task ApplyBootstrapAsync(bool includeCatalog)
        {
            var uri = includeCatalog
                ? "api/sync/bootstrap?includeCatalog=true"
                : "api/sync/bootstrap";

            var response = await Client.GetAsync(uri);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
            var bootstrap = await response.Content.ReadFromJsonAsync<StoreBootstrapSyncDto>()
                ?? new StoreBootstrapSyncDto();

            await _productOffline.UpsertCatalogDeltaAsync(
                bootstrap.Categories,
                bootstrap.Products,
                bootstrap.StoreProducts,
                bootstrap.ProductVariants);

            if (bootstrap.StoreId.HasValue)
            {
                await _productOffline.UpsertSellableVariantsAsync(
                    bootstrap.StoreId.Value,
                    bootstrap.SellableVariants,
                    Array.Empty<int>());
                await _productOffline.ApplyStockLevelDeltaAsync(bootstrap.StoreId.Value, bootstrap.StockLevels);

                await _productOffline.SetCursorAsync(GetSellableCursorKey(bootstrap.StoreId.Value), bootstrap.SellableVariantsCursor ?? bootstrap.ServerUtcNow);
                await _productOffline.SetCursorAsync(GetStockCursorKey(bootstrap.StoreId.Value), bootstrap.StockCursor ?? bootstrap.ServerUtcNow);
                await _salesOffline.SetCursorAsync(GetSalesCursorKey(bootstrap.StoreId.Value), bootstrap.SalesCursor ?? bootstrap.ServerUtcNow);
            }

            if (bootstrap.TenantId.HasValue)
                await _productOffline.SetCursorAsync(GetCatalogCursorKey(bootstrap.TenantId.Value), bootstrap.CatalogCursor ?? bootstrap.ServerUtcNow);
        }

        private async Task SyncCatalogDeltaAsync(int tenantId)
        {
            var cursorKey = GetCatalogCursorKey(tenantId);
            var updatedAfter = await _productOffline.GetCursorAsync(cursorKey);
            var uri = updatedAfter.HasValue
                ? $"api/sync/catalog?tenantId={tenantId}&updatedAfter={FormatCursor(updatedAfter.Value)}"
                : $"api/sync/catalog?tenantId={tenantId}";

            var response = await Client.GetAsync(uri);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
            var delta = await response.Content.ReadFromJsonAsync<CatalogDeltaDto>() ?? new CatalogDeltaDto();

            await _productOffline.UpsertCatalogDeltaAsync(
                delta.Categories,
                delta.Products,
                delta.StoreProducts,
                delta.ProductVariants);

            await _productOffline.SetCursorAsync(cursorKey, delta.Cursor == default ? delta.ServerUtcNow : delta.Cursor);
        }

        private async Task SyncSellableVariantsDeltaAsync(int storeId)
        {
            var cursorKey = GetSellableCursorKey(storeId);
            var updatedAfter = await _productOffline.GetCursorAsync(cursorKey);
            var uri = updatedAfter.HasValue
                ? $"api/sync/sellable-variants?storeId={storeId}&updatedAfter={FormatCursor(updatedAfter.Value)}"
                : $"api/sync/sellable-variants?storeId={storeId}";

            var response = await Client.GetAsync(uri);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
            var delta = await response.Content.ReadFromJsonAsync<StoreSellableVariantsDeltaDto>()
                ?? new StoreSellableVariantsDeltaDto();

            await _productOffline.UpsertSellableVariantsAsync(storeId, delta.Items, delta.RemovedVariantIds);
            await _productOffline.SetCursorAsync(cursorKey, delta.Cursor == default ? delta.ServerUtcNow : delta.Cursor);
        }

        private async Task SyncStockDeltaAsync(int storeId)
        {
            var cursorKey = GetStockCursorKey(storeId);
            var updatedAfter = await _productOffline.GetCursorAsync(cursorKey);
            var uri = updatedAfter.HasValue
                ? $"api/sync/stock-levels?storeId={storeId}&updatedAfter={FormatCursor(updatedAfter.Value)}"
                : $"api/sync/stock-levels?storeId={storeId}";

            var response = await Client.GetAsync(uri);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
            var delta = await response.Content.ReadFromJsonAsync<StockLevelsDeltaDto>()
                ?? new StockLevelsDeltaDto();

            await _productOffline.ApplyStockLevelDeltaAsync(storeId, delta.Items);
            await _productOffline.SetCursorAsync(cursorKey, delta.Cursor == default ? delta.ServerUtcNow : delta.Cursor);
        }

        private bool ShouldIncludeCatalogBootstrap() =>
            _auth.IsTenantAdmin || _auth.IsStoreManager || !_auth.SelectedStoreId.HasValue;

        private static string GetCatalogCursorKey(int tenantId) => $"catalog:{tenantId}";
        private static string GetSellableCursorKey(int storeId) => $"sellable:{storeId}";
        private static string GetStockCursorKey(int storeId) => $"stock:{storeId}";
        private static string GetSalesCursorKey(int storeId) => $"sales:{storeId}";

        private static string FormatCursor(DateTime cursor)
        {
            var utc = cursor.Kind switch
            {
                DateTimeKind.Utc => cursor,
                DateTimeKind.Local => cursor.ToUniversalTime(),
                _ => DateTime.SpecifyKind(cursor, DateTimeKind.Utc),
            };
            return Uri.EscapeDataString(utc.ToString("O"));
        }
    }
}
