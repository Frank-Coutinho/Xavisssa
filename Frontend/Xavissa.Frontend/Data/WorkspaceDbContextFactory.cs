using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data;

public class WorkspaceDbContextFactory : IDbContextFactory<LocalDbContext>
{
    private readonly IWorkspaceService _workspace;

    public WorkspaceDbContextFactory(IWorkspaceService workspace)
    {
        _workspace = workspace;
    }

    public LocalDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite(LocalDbContext.BuildConnectionString(_workspace.CurrentDbPath))
            .Options;
        return new LocalDbContext(options);
    }
}
