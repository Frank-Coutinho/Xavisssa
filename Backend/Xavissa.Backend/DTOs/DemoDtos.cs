namespace Xavissa.Backend.DTOs;

public class DemoStartRequest
{
    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }
    public string? MachineUserName { get; set; }
    public string? AppVersion { get; set; }
    public string? OSVersion { get; set; }
    public string? OptionalLeadName { get; set; }
    public string? OptionalLeadPhone { get; set; }
    public string? OptionalLeadEmail { get; set; }
    public string? DemoTemplateCode { get; set; }
    public bool UseDefaultTemplate { get; set; } = true;
    public bool ResetOnClose { get; set; } = true;
}

public class DemoStartResponse
{
    public bool Success { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public int? DemoSessionId { get; set; }
    public int? TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool ResetOnClose { get; set; }
    public bool DemoModeEnabled { get; set; }
    public string DemoToken { get; set; } = string.Empty;
}

public class ValidateDemoSessionRequest
{
    public int? DemoSessionId { get; set; }
    public int? TenantId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
}

public class ValidateDemoSessionResponse
{
    public bool Success { get; set; }
    public bool IsExpired { get; set; }
    public string? FailureMessage { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int RemainingSeconds { get; set; }
}

public class DemoSessionEventRequest
{
    public int? DemoSessionId { get; set; }
    public string DemoToken { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Description { get; set; }
}
