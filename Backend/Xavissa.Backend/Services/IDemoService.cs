using Xavissa.Backend.DTOs;

namespace Xavissa.Backend.Services;

public interface IDemoService
{
    Task<DemoStartResponse> StartDemoAsync(DemoStartRequest request, string? ipAddress);
    Task<ValidateDemoSessionResponse> ValidateDemoAsync(ValidateDemoSessionRequest request);
    Task<bool> TrackEventAsync(DemoSessionEventRequest request);
}
