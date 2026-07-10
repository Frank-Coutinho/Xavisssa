using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface IDemoWorkspaceSeeder
{
    Task SeedAsync(StartDemoSessionResponse response);
}
