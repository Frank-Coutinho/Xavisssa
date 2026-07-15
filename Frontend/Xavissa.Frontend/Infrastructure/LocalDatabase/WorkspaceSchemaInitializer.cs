using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;

namespace Xavissa.Frontend.Infrastructure.LocalDatabase;

public sealed class WorkspaceSchemaInitializer(
    IDbContextFactory<LocalDbContext> dbContextFactory) : IWorkspaceSchemaInitializer
{
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.CloseConnectionAsync();
        await db.EnsureLocalSchemaAsync();
    }
}
