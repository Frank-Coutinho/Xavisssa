using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public sealed class StockAvailabilityService : IStockAvailabilityService
{
    private readonly IConnectivityService _connectivity;
    private readonly IApiTokenProvider _tokens;
    private readonly IHttpClientFactory _httpClientFactory;

    public StockAvailabilityService(
        IConnectivityService connectivity,
        IApiTokenProvider tokens,
        IHttpClientFactory httpClientFactory)
    {
        _connectivity = connectivity;
        _tokens = tokens;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<LiveStockAvailabilityResult> GetLiveAvailabilityAsync(
        int storeId,
        IEnumerable<int> variantIds)
    {
        var ids = variantIds.Where(id => id > 0).Distinct().ToList();
        if (storeId <= 0 || ids.Count == 0)
        {
            return new LiveStockAvailabilityResult
            {
                IsAvailable = false,
                Error = "A store and at least one product variant are required for a live stock check.",
            };
        }

        if (!_connectivity.IsOnline() || string.IsNullOrWhiteSpace(_tokens.Token))
        {
            return new LiveStockAvailabilityResult
            {
                IsAvailable = false,
                Error = "Critical stock requires an authenticated server connection before the sale can continue.",
            };
        }

        try
        {
            var client = _httpClientFactory.CreateClient("backend");
            var response = await client.PostAsJsonAsync(
                "api/sync/stock-check",
                new LiveStockCheckRequestDto { StoreId = storeId, VariantIds = ids });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new LiveStockAvailabilityResult
                {
                    IsAvailable = false,
                    Error = "Your online session expired. Sign in again to verify critical stock.",
                };
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LiveStockCheckResponseDto>()
                ?? new LiveStockCheckResponseDto();

            return new LiveStockAvailabilityResult
            {
                IsAvailable = true,
                QuantityByVariantId = result.Items
                    .GroupBy(item => item.VariantId)
                    .ToDictionary(group => group.Key, group => group.First().QuantityOnHand),
            };
        }
        catch (Exception ex)
        {
            return new LiveStockAvailabilityResult
            {
                IsAvailable = false,
                Error = $"The server stock check failed: {ex.Message}",
            };
        }
    }
}
