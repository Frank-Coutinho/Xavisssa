using System;
using System.Net;

namespace Xavissa.Frontend.Services;

public sealed class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, string message, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }
    public bool IsSessionExpired => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsPermissionDenied => StatusCode == HttpStatusCode.Forbidden;
    public bool IsValidationOrBusinessError => StatusCode == HttpStatusCode.BadRequest;
    public bool IsStaleOrMissing => StatusCode == HttpStatusCode.NotFound;
    public bool IsConflict => StatusCode == HttpStatusCode.Conflict;
    public bool IsServerError => (int)StatusCode >= 500;
}
