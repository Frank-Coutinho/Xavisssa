using Microsoft.Extensions.Options;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public sealed class SyncConflictHandler : ISyncConflictHandler
{
    private readonly INotificationService _notifications;
    private readonly OfflineFirstOptions _options;

    public SyncConflictHandler(
        INotificationService notifications,
        IOptions<OfflineFirstOptions> options)
    {
        _notifications = notifications;
        _options = options.Value;
    }

    public void HandleSaleConflict(SaleSyncConflictNotice conflict)
    {
        var action = _options.StockConflictPolicy.Equals("AlertStaff", System.StringComparison.OrdinalIgnoreCase)
            ? "Stop fulfilment and issue or refund the customer's payment according to store procedure."
            : "This sale needs staff review before fulfilment.";
        var reason = string.IsNullOrWhiteSpace(conflict.Error)
            ? "The central server rejected the sale, usually because another device sold the remaining stock first."
            : conflict.Error;

        _notifications.Show(
            $"Stock conflict on local sale #{conflict.LocalSaleId}: {reason} {action}",
            NotificationType.Error,
            12000);
    }
}
