namespace Xavissa.Backend.Security;

public interface IRlsContextService
{
    Task ApplyAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
