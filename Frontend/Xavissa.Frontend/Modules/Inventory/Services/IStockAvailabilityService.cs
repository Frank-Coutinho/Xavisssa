using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xavissa.Frontend.Services;

public interface IStockAvailabilityService
{
    Task<LiveStockAvailabilityResult> GetLiveAvailabilityAsync(
        int storeId,
        IEnumerable<int> variantIds);
}

public sealed class LiveStockAvailabilityResult
{
    public bool IsAvailable { get; init; }
    public string? Error { get; init; }
    public IReadOnlyDictionary<int, int> QuantityByVariantId { get; init; }
        = new Dictionary<int, int>();
}
