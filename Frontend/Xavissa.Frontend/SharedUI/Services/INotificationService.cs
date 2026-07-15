public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
}

public interface INotificationService
{
    void Show(string message, NotificationType type = NotificationType.Info, int durationMs = 2500);
}
