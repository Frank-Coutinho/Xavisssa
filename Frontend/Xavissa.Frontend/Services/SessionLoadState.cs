using System;

namespace Xavissa.Frontend.Services
{
    public class SessionLoadState : ISessionLoadState
    {
        private bool _isBusy;
        private string _statusText = string.Empty;

        public bool IsBusy => _isBusy;
        public string StatusText => _statusText;

        public event Action? Changed;
        public event Action? OnlineDataApplied;

        public void Show(string statusText)
        {
            _isBusy = true;
            _statusText = statusText;
            Changed?.Invoke();
        }

        public void Hide()
        {
            _isBusy = false;
            _statusText = string.Empty;
            Changed?.Invoke();
        }

        public void NotifyOnlineDataApplied()
        {
            OnlineDataApplied?.Invoke();
        }
    }
}
