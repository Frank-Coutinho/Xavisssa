namespace Xavissa.Database.Models;

public class DemoSessionEvent
{
    public long Id { get; set; }
    public int DemoSessionId { get; set; }
    public DemoSession DemoSession { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
