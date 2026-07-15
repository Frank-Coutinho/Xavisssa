using System.Threading.Tasks;

namespace Xavissa.Frontend.Services
{
    public interface IConfirmationDialogService
    {
        Task<bool> ConfirmDeleteAsync(string title, string message);
        Task<bool> ConfirmActionAsync(string title, string message, string confirmText, bool isDestructive);
    }
}
