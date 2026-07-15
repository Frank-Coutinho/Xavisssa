using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface IDemoApiClient
{
    Task<StartDemoSessionResponse> StartDemoSessionAsync(StartDemoSessionRequest request);
    Task<ValidateDemoSessionResponse> ValidateDemoSessionAsync(ValidateDemoSessionRequest request);
    Task<bool> EndDemoSessionAsync(EndDemoSessionRequest request);
    Task<bool> TrackDemoEventAsync(TrackDemoEventRequest request);
}
