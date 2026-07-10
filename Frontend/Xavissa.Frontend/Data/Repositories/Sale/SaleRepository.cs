using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Mappers;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data.Repositories
{
    public class SaleRepository : ISaleRepository
    {
        private readonly ISaleOfflineRepository _offline;
        private readonly ISaleOnlineRepository _online;
        private readonly IConnectivityService _net;
        private readonly IApiTokenProvider _tokens;
        private readonly IProductRepository _products;
        private readonly IAuthService _auth;
        private readonly IHttpClientFactory _httpFactory;

        public event Action? SalesChanged;

        public SaleRepository(
            ISaleOfflineRepository offline,
            ISaleOnlineRepository online,
            IConnectivityService net,
            IApiTokenProvider tokens,
            IProductRepository products,
            IAuthService auth,
            IHttpClientFactory httpFactory
        )
        {
            _offline = offline ?? throw new ArgumentNullException(nameof(offline));
            _online = online ?? throw new ArgumentNullException(nameof(online));
            _net = net ?? throw new ArgumentNullException(nameof(net));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        }

        private bool CanUseAuthenticatedOnline() =>
            _net.IsOnline() && !string.IsNullOrWhiteSpace(_tokens.Token);

        private HttpClient Client => _httpFactory.CreateClient("backend");

        public Task<List<Sale>> GetAllAsync() => _offline.GetAllAsync();

        public Task<List<Sale>> GetHistoryPageAsync(SaleHistoryQuery query) =>
            _offline.GetHistoryPageAsync(query);

        public async Task SoftDeleteSaleAsync(int saleId, string reason)
        {
            if (!CanUseAuthenticatedOnline())
                throw new InvalidOperationException("Internet and an authenticated session are required.");

            await _online.SoftDeleteSaleAsync(saleId, reason);
            await SyncAsync();
        }

        public async Task RefundSaleAsync(int saleId, string reason)
        {
            if (!CanUseAuthenticatedOnline())
                throw new InvalidOperationException("Internet and an authenticated session are required.");

            await _online.RefundSaleAsync(saleId, reason);
            await SyncAsync();
        }

        public async Task RefundSaleItemAsync(int saleId, int saleItemId, int quantity, string reason)
        {
            if (!CanUseAuthenticatedOnline())
                throw new InvalidOperationException("Internet and an authenticated session are required.");

            await _online.RefundSaleItemAsync(saleId, saleItemId, quantity, reason);
            await SyncAsync();
        }

        public async Task<Sale> CreateAsync(Sale sale)
        {
            if (sale == null)
                throw new ArgumentNullException(nameof(sale));

            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    var serverSale = await _online.CreateAsync(sale);
                    serverSale.Synced = true;
                    await _offline.UpsertRangeAsync(new[] { serverSale });
                    SalesChanged?.Invoke();
                    return serverSale;
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("⚠️ Authenticated online sale creation was denied. Saving sale locally for retry after re-login.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Online sale creation failed. Saving locally for retry: {ex.Message}");
                }
            }

            sale.Synced = false;
            await _offline.AddAsync(sale);
            SalesChanged?.Invoke();
            return sale;
        }

        public async Task SyncAsync()
        {
            if (!CanUseAuthenticatedOnline())
                return;

            var anyChanges = false;
            var unsynced = await _offline.GetUnsyncedAsync();

            if (unsynced.Count > 0)
            {
                var uploadRequest = new SalesUploadBatchRequestDto
                {
                    Sales = new List<PendingSaleUploadDto>(),
                };

                foreach (var sale in unsynced)
                {
                    try
                    {
                        uploadRequest.Sales.Add(new PendingSaleUploadDto
                        {
                            ClientSaleId = sale.Id,
                            Sale = await MapToSaleSyncDtoAsync(sale),
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Failed to prepare sale {sale.Id} for sync: {ex.Message}");
                        await _offline.MarkAsFailedAsync(sale.Id);
                    }
                }

                if (uploadRequest.Sales.Count > 0)
                {
                    var uploadResponse = await Client.PostAsJsonAsync("api/sync/sales/upload", uploadRequest);
                    if (uploadResponse.StatusCode == HttpStatusCode.Unauthorized)
                        throw new UnauthorizedAccessException();

                    uploadResponse.EnsureSuccessStatusCode();
                    var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<SalesUploadBatchResultDto>()
                        ?? new SalesUploadBatchResultDto();

                    foreach (var result in uploadResult.Results)
                    {
                        if (result.Success && result.ServerSaleId.HasValue)
                        {
                            await _offline.MarkAsSyncedAsync(
                                result.ClientSaleId,
                                result.ServerSaleId.Value,
                                result.SyncId);
                            anyChanges = true;
                        }
                        else
                        {
                            await _offline.MarkAsFailedAsync(result.ClientSaleId);
                        }
                    }
                }
            }

            var storeId = _auth.SelectedStoreId
                ?? unsynced.Select(sale => sale.StoreId).FirstOrDefault(id => id > 0);
            if (storeId <= 0)
                return;

            var cursorKey = GetSalesCursorKey(storeId);
            var updatedAfter = await _offline.GetCursorAsync(cursorKey);
            var requestUri = updatedAfter.HasValue
                ? $"api/sync/sales?storeId={storeId}&updatedAfter={FormatCursor(updatedAfter.Value)}"
                : $"api/sync/sales?storeId={storeId}";

            var deltaResponse = await Client.GetAsync(requestUri);
            if (deltaResponse.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            deltaResponse.EnsureSuccessStatusCode();
            var delta = await deltaResponse.Content.ReadFromJsonAsync<SalesDeltaDto>() ?? new SalesDeltaDto();

            if (delta.Sales.Count > 0)
            {
                await _offline.UpsertRangeAsync(delta.Sales.Select(SaleMapper.FromReadDto));
                anyChanges = true;
            }

            if (delta.DeletedSaleIds.Count > 0)
            {
                await _offline.DeleteRangeAsync(delta.DeletedSaleIds);
                anyChanges = true;
            }

            await _offline.SetCursorAsync(cursorKey, delta.Cursor == default ? delta.ServerUtcNow : delta.Cursor);

            if (anyChanges)
                SalesChanged?.Invoke();
        }

        private async Task<SaleSyncDto> MapToSaleSyncDtoAsync(Sale sale)
        {
            if (sale.Items == null || sale.Items.Count == 0)
                throw new InvalidOperationException($"Sale {sale.Id} has no items.");

            var localProductIds = sale.Items
                .Select(item => item.ProductId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var localProducts = await _products.GetByIdsAsync(localProductIds);
            var productsById = localProducts.ToDictionary(product => product.Id);

            var items = new List<SaleItemSyncDto>();
            foreach (var item in sale.Items)
            {
                Product? product = null;
                if (item.ProductId > 0)
                    productsById.TryGetValue(item.ProductId, out product);

                var variantId = item.VariantId > 0 ? item.VariantId : product?.VariantId ?? 0;
                int? productId = product?.OnlineId > 0
                    ? product.OnlineId
                    : (item.ProductId > 0 ? item.ProductId : (int?)null);

                if (variantId <= 0)
                    throw new InvalidOperationException($"Sale item {item.Id} is missing a synced variant.");

                items.Add(new SaleItemSyncDto
                {
                    OnlineId = item.OnlineId > 0 ? item.OnlineId : null,
                    SyncId = item.SyncId,
                    TenantId = sale.TenantId,
                    StoreId = sale.StoreId,
                    SourceDeviceId = item.SourceDeviceId ?? sale.SourceDeviceId,
                    ClientCreatedAt = item.ClientCreatedAt,
                    ClientUpdatedAt = item.ClientUpdatedAt,
                    LastSyncedAt = item.LastSyncedAt,
                    VariantId = variantId,
                    ProductId = productId,
                    Quantity = item.Quantity,
                    IsRefunded = item.IsRefunded,
                    RefundReason = item.RefundReason,
                });
            }

            return new SaleSyncDto
            {
                OnlineId = sale.OnlineId > 0 ? sale.OnlineId : null,
                SyncId = sale.SyncId,
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                SourceDeviceId = sale.SourceDeviceId,
                ClientCreatedAt = sale.ClientCreatedAt,
                ClientUpdatedAt = sale.ClientUpdatedAt,
                LastSyncedAt = sale.LastSyncedAt,
                SaleItems = items,
                SalePayments = sale.Payments.Select(payment => new SalePaymentSyncDto
                {
                    OnlineId = payment.OnlineId > 0 ? payment.OnlineId : null,
                    SyncId = payment.SyncId,
                    TenantId = payment.TenantId,
                    StoreId = payment.StoreId,
                    SourceDeviceId = payment.SourceDeviceId ?? sale.SourceDeviceId,
                    ClientCreatedAt = payment.ClientCreatedAt,
                    ClientUpdatedAt = payment.ClientUpdatedAt,
                    LastSyncedAt = payment.LastSyncedAt,
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    ReferenceNumber = payment.ReferenceNumber,
                    Notes = payment.Notes,
                }).ToList(),
                Discount = sale.Discount,
            };
        }

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
