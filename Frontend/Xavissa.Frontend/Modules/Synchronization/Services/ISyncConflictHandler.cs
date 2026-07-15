namespace Xavissa.Frontend.Services;

public interface ISyncConflictHandler
{
    void HandleSaleConflict(SaleSyncConflictNotice conflict);
}

public sealed record SaleSyncConflictNotice(
    int LocalSaleId,
    int? ServerConflictId,
    string? Error);
