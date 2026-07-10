namespace Xavissa.Database.Security
{
    public interface IRequestContext
    {
        bool IsAuthenticated { get; }
        int? UserId { get; }
        string PlatformRole { get; }
        bool IsPlatformAdmin { get; }
        string? ActingRole { get; }
        IReadOnlyCollection<int> AllowedTenantIds { get; }
        IReadOnlyCollection<int> AllowedStoreIds { get; }
        int? SelectedTenantId { get; }
        int? SelectedStoreId { get; }
        string? IpAddress { get; }
    }
}
