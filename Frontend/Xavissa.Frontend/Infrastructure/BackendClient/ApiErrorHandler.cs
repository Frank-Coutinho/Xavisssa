using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Xavissa.Frontend.Services;

public interface IApiErrorHandler
{
    Task EnsureSuccessAsync(HttpResponseMessage response, string? fallbackMessage = null);
    void Notify(ApiException exception);
}

public sealed class ApiErrorHandler : IApiErrorHandler
{
    private readonly IAuthService _auth;
    private readonly INotificationService _notifications;

    public ApiErrorHandler(IAuthService auth, INotificationService notifications)
    {
        _auth = auth;
        _notifications = notifications;
    }

    public async Task EnsureSuccessAsync(HttpResponseMessage response, string? fallbackMessage = null)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        var message = BuildMessage(response.StatusCode, body, fallbackMessage);
        var exception = new ApiException(response.StatusCode, message, body);

        if (exception.IsSessionExpired)
            _auth.NotifySessionExpired();

        throw exception;
    }

    public void Notify(ApiException exception)
    {
        var type = exception.IsPermissionDenied || exception.IsValidationOrBusinessError
            ? NotificationType.Warning
            : NotificationType.Error;

        _notifications.Show(exception.Message, type, exception.IsServerError ? 4000 : 3000);
    }

    private static string BuildMessage(HttpStatusCode statusCode, string body, string? fallbackMessage)
    {
        if (!string.IsNullOrWhiteSpace(body))
            return TrimBody(body);

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            HttpStatusCode.Forbidden => "Permission or workspace scope denied.",
            HttpStatusCode.BadRequest => fallbackMessage ?? "The backend rejected the request.",
            HttpStatusCode.NotFound => "The requested record was not found. Local cached data may be stale.",
            HttpStatusCode.Conflict => "A conflict was detected. Open Sync Conflicts to resolve it.",
            _ when (int)statusCode >= 500 => "The backend is temporarily unavailable. Local data was kept for retry.",
            _ => fallbackMessage ?? $"Backend request failed with status {(int)statusCode}.",
        };
    }

    private static string TrimBody(string body)
    {
        var text = body.Trim();
        return text.Length <= 500 ? text : text[..500];
    }
}
