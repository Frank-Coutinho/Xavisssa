using System.Threading.Tasks;
using System.Collections.Generic;
using Xavissa.Frontend.Models.Auth;

public interface ILoginCoordinator
{
    bool HasPendingStoreSelection { get; }
    IReadOnlyList<AssignedStore> PendingStoreChoices { get; }
    Task<bool> LoginAsync(string username, string password);
    Task<bool> CompletePendingStoreSelectionAsync(int storeId);
    Task<bool> TryUpgradeSessionOnlineAsync(string username, string password, int maxAttempts = 5);
}
