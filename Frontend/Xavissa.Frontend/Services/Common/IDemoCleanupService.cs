using System.Threading.Tasks;

namespace Xavissa.Frontend.Services;

public interface IDemoCleanupService
{
    Task CleanupExpiredDemoAsync();
    Task CleanupOnCloseAsync();
}
