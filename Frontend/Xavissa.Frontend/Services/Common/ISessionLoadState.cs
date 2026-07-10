using System;

namespace Xavissa.Frontend.Services
{
    public interface ISessionLoadState
    {
        bool IsBusy { get; }
        string StatusText { get; }
        event Action? Changed;
        event Action? OnlineDataApplied;
        void Show(string statusText);
        void Hide();
        void NotifyOnlineDataApplied();
    }
}
