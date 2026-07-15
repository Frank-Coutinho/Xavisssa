using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class DemoApiClient : IDemoApiClient
{
    private const string TemporaryDemoSignature = "TEMPORARY-UNSIGNED-DEMO-SNAPSHOT";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LicensingOptions _options;

    public DemoApiClient(IHttpClientFactory httpClientFactory, IOptions<LicensingOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<StartDemoSessionResponse> StartDemoSessionAsync(StartDemoSessionRequest request)
    {
        request.ResetOnClose = _options.DemoResetOnClose;
        request.DemoTemplateCode ??= _options.DemoTemplateCode ?? _options.DefaultDemoTemplate;

        if (_options.DemoApiBaseUrl == null)
            return BuildLocalDevelopmentSession(request, "LOCAL_DEMO_FALLBACK");

        try
        {
            var response = await Client.PostAsJsonAsync("api/demo/start", request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<StartDemoSessionResponse>() ?? BuildFailure("EMPTY_RESPONSE", "The demo API returned an empty response.");

            var body = await response.Content.ReadAsStringAsync();
            return BuildFailure(response.StatusCode.ToString(), string.IsNullOrWhiteSpace(body) ? "Demo session could not be started." : body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return BuildFailure("SERVER_UNAVAILABLE", "The demo API is unavailable.");
        }
    }

    public async Task<ValidateDemoSessionResponse> ValidateDemoSessionAsync(ValidateDemoSessionRequest request)
    {
        if (_options.DemoApiBaseUrl == null)
            return BuildLocalValidation(request);

        try
        {
            var response = await Client.PostAsJsonAsync("api/demo/validate", request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<ValidateDemoSessionResponse>() ?? new ValidateDemoSessionResponse { Success = false, FailureMessage = "The demo API returned an empty response." };

            return new ValidateDemoSessionResponse { Success = false, FailureMessage = await response.Content.ReadAsStringAsync() };
        }
        catch
        {
            return new ValidateDemoSessionResponse { Success = false, FailureMessage = "The demo API is unavailable." };
        }
    }

    public async Task<bool> EndDemoSessionAsync(EndDemoSessionRequest request)
    {
        if (_options.DemoApiBaseUrl == null)
            return true;

        try
        {
            var response = await Client.PostAsJsonAsync("api/demo/end", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TrackDemoEventAsync(TrackDemoEventRequest request)
    {
        if (_options.DemoApiBaseUrl == null)
            return true;

        try
        {
            var response = await Client.PostAsJsonAsync("api/demo/events", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private HttpClient Client => _httpClientFactory.CreateClient("demo");

    private StartDemoSessionResponse BuildLocalDevelopmentSession(StartDemoSessionRequest request, string token)
    {
        var now = DateTime.UtcNow;
        var duration = Math.Max(1, _options.DemoDurationMinutes);
        var expiresAt = now.AddMinutes(duration);
        var demoSessionId = Random.Shared.Next(100000, 999999);
        var tenantId = 1000001;
        var licenseId = 1000001;

        var snapshot = new LocalLicenseSnapshot
        {
            TenantId = tenantId,
            TenantCode = "DEMO-MZ",
            TenantName = "Loja Demo Xavissa",
            LicenseId = licenseId,
            LicensePublicCode = "XAV-DEMO-LOCAL",
            LicensePlanId = 0,
            PlanCode = "DEMO",
            PlanName = "Public Demo",
            ActivationId = demoSessionId,
            DeviceFingerprint = request.DeviceFingerprint,
            Status = "Active",
            IsDemo = true,
            IsTrial = false,
            LicenseType = "Demo",
            PurchaseType = "Demo",
            MaxStores = 2,
            MaxUsers = 3,
            MaxDevices = 1,
            MaxOfflineDays = 0,
            AllowsMultiStore = true,
            AllowsAdvancedReports = true,
            AllowsCloudSync = false,
            AllowsBarcodePrinting = true,
            AllowsCustomReceipt = true,
            AllowsDemoMode = true,
            IssuedAt = now,
            ActivatedAt = now,
            ExpiresAt = expiresAt,
            LastValidatedAt = now,
            GracePeriodEndsAt = expiresAt,
            SnapshotIssuedAt = now,
            SnapshotExpiresAt = expiresAt,
            Signature = TemporaryDemoSignature,
        };

        return new StartDemoSessionResponse
        {
            Success = true,
            DemoSessionId = demoSessionId,
            TenantId = tenantId,
            TenantCode = snapshot.TenantCode,
            TenantName = snapshot.TenantName,
            LicenseId = licenseId,
            StartedAt = now,
            ExpiresAt = expiresAt,
            ResetOnClose = _options.DemoResetOnClose,
            DemoLicenseSnapshot = snapshot,
            DemoModeEnabled = true,
            DemoToken = token,
        };
    }

    private static StartDemoSessionResponse BuildFailure(string code, string message) =>
        new()
        {
            Success = false,
            FailureCode = code,
            FailureMessage = message,
        };

    private static ValidateDemoSessionResponse BuildLocalValidation(ValidateDemoSessionRequest request) =>
        new()
        {
            Success = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            RemainingSeconds = 3600,
            IsExpired = false,
        };
}
