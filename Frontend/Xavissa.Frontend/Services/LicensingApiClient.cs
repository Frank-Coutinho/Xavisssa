using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class LicensingApiClient : ILicensingApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LicensingOptions _options;

    public LicensingApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<LicensingOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public Task<ActivateLicenseResponse> ActivateLicenseAsync(ActivateLicenseRequest request) =>
        PostAsync<ActivateLicenseRequest, ActivateLicenseResponse>("api/licensing/activate", request);

    public Task<ValidateActivationResponse> ValidateActivationAsync(ValidateActivationRequest request) =>
        PostAsync<ValidateActivationRequest, ValidateActivationResponse>("api/licensing/validate-activation", request);

    public Task<StartDemoSessionResponse> StartDemoSessionAsync(StartDemoSessionRequest request) =>
        PostAsync<StartDemoSessionRequest, StartDemoSessionResponse>("api/demo/start", request);

    public Task<ValidateActivationResponse> RefreshLicenseSnapshotAsync(ValidateActivationRequest request) =>
        PostAsync<ValidateActivationRequest, ValidateActivationResponse>("api/licensing/refresh-snapshot", request);

    public async Task<bool> DeactivateCurrentDeviceAsync(DeactivateCurrentDeviceRequest request)
    {
        if (!HasConfiguredEndpoint())
            return false;

        try
        {
            var response = await Client.PostAsJsonAsync("api/licensing/deactivate-current-device", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<EffectiveLicenseLimitsDto?> GetTenantLicenseStatusAsync(int tenantId)
    {
        if (!HasConfiguredEndpoint())
            return null;

        try
        {
            return await Client.GetFromJsonAsync<EffectiveLicenseLimitsDto>($"api/licensing/tenant/{tenantId}/status");
        }
        catch
        {
            return null;
        }
    }

    private HttpClient Client => _httpClientFactory.CreateClient("licensing");

    private bool HasConfiguredEndpoint() => _options.LicensingApiBaseUrl != null;

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest request)
        where TResponse : new()
    {
        if (!HasConfiguredEndpoint())
            return BuildFailure<TResponse>("SERVER_UNAVAILABLE", "Licensing API is not configured.");

        try
        {
            var response = await Client.PostAsJsonAsync(endpoint, request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<TResponse>() ?? new TResponse();

            var body = await response.Content.ReadAsStringAsync();
            return BuildFailure<TResponse>(response.StatusCode.ToString(), string.IsNullOrWhiteSpace(body) ? "Licensing request failed." : body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return BuildFailure<TResponse>("SERVER_UNAVAILABLE", "The licensing server is unavailable.");
        }
    }

    private static TResponse BuildFailure<TResponse>(string code, string message)
        where TResponse : new()
    {
        var response = new TResponse();
        switch (response)
        {
            case ActivateLicenseResponse activate:
                activate.Success = false;
                activate.FailureCode = code;
                activate.FailureMessage = message;
                break;
            case ValidateActivationResponse validate:
                validate.Success = false;
                validate.FailureCode = code;
                validate.FailureMessage = message;
                validate.ShouldBlockWorkspace = code != "SERVER_UNAVAILABLE";
                break;
            case StartDemoSessionResponse demo:
                demo.Success = false;
                demo.FailureMessage = message;
                break;
        }

        return response;
    }
}
