using System.Threading;
using System.Threading.Tasks;

namespace Xavissa.Frontend.Infrastructure.LocalDatabase;

public interface IWorkspaceSchemaInitializer
{
    Task EnsureAsync(CancellationToken cancellationToken = default);
}
