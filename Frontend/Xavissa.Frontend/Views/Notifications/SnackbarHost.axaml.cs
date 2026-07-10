using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Views.Notifications
{
    public partial class SnackbarHost : UserControl
    {
        private const int MaxStack = 4;
        private readonly List<Border> _activeToasts = new();

        public SnackbarHost()
        {
            InitializeComponent();
            NotificationService.OnShow += HandleNotification;
        }

        private void HandleNotification(string message, NotificationType type, int duration)
        {
            _ = ShowToastAsync(message, type, duration);
        }

        private async Task ShowToastAsync(string message, NotificationType type, int duration)
        {
            Border toast = null!;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                toast = CreateToast(message, type);

                PART_Container.Children.Insert(0, toast);
                _activeToasts.Insert(0, toast);

                // Enforce max stack size
                if (_activeToasts.Count > MaxStack)
                {
                    var oldest = _activeToasts[^1];

                    _activeToasts.RemoveAt(_activeToasts.Count - 1);
                    PART_Container.Children.Remove(oldest);
                }
            });

            // Instant appear (no animation)
            InstantShow(toast);

            await Task.Delay(duration);

            await AnimateOut(toast);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_activeToasts.Contains(toast))
                {
                    PART_Container.Children.Remove(toast);
                    _activeToasts.Remove(toast);
                }
            });
        }

        private Border CreateToast(string message, NotificationType type)
        {
            return new Border
            {
                Background = GetBrush(type),
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(8),
                Opacity = 0,
                RenderTransform = new TranslateTransform { Y = 0 },
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 16,
                },
            };
        }

        private void InstantShow(Control control)
        {
            control.Opacity = 1;

            if (control.RenderTransform is TranslateTransform tt)
                tt.Y = 0;
        }

        private async Task AnimateOut(Control control)
        {
            if (control.RenderTransform is not TranslateTransform tt)
                return;

            const int duration = 250;
            const int frames = 25;

            for (int i = 0; i <= frames; i++)
            {
                double progress = (double)i / frames;

                control.Opacity = 1 - progress;
                tt.Y = 40 * progress;

                await Task.Delay(duration / frames);
            }

            control.Opacity = 0;
            tt.Y = 40;
        }

        private IBrush GetBrush(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => Brushes.Green,
                NotificationType.Warning => Brushes.Orange,
                NotificationType.Error => Brushes.Red,
                _ => Brushes.Black,
            };
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            NotificationService.OnShow -= HandleNotification;
            base.OnDetachedFromVisualTree(e);
        }
    }
}
