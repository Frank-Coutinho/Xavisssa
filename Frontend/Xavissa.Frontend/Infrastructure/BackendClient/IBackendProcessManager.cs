using System.Threading.Tasks;

namespace Xavissa.Frontend.Services
{
    public interface IBackendProcessManager
    {
        Task StartAsync();
        Task StopAsync();
    }
}
