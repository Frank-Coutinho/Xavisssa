using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Xavissa.Frontend.Views.Dialogs;

namespace Xavissa.Frontend.Services
{
    public class ConfirmationDialogService : IConfirmationDialogService
    {
        public Task<bool> ConfirmDeleteAsync(string title, string message) =>
            ConfirmActionAsync(title, message, "Delete", true);

        public async Task<bool> ConfirmActionAsync(string title, string message, string confirmText, bool isDestructive)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ConfirmationDialog();
                dialog.Configure(title, message, confirmText, isDestructive);

                var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                var result = owner == null
                    ? await dialog.ShowDialog<bool?>(null)
                    : await dialog.ShowDialog<bool?>(owner);

                return result == true;
            });
        }
    }
}
