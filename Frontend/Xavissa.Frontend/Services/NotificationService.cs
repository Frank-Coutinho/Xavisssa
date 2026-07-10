using System;

namespace Xavissa.Frontend.Services
{
    public class NotificationService : INotificationService
    {
        // GLOBAL STATIC EVENT FOR AVAILABILITY ANYWHERE (AppView listens here)
        public static event Action<string, NotificationType, int>? OnShow;

        public void Show(
            string message,
            NotificationType type = NotificationType.Info,
            int durationMs = 2500
        )
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // You can customize duration based on type if needed
            int ms = durationMs switch
            {
                <= 0 => 2500,
                _ => durationMs,
            };

            // Raise the global event
            OnShow?.Invoke(message, type, ms);
        }
    }
}
