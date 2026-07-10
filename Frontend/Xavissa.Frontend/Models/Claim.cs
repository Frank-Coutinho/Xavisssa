using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Xavissa.Frontend.Models
{
    public class Claim
    {
        public int Id { get; set; }
        public string type { get; set; } = string.Empty;
        public string value { get; set; } = string.Empty;
    }

    public class User : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int OnlineUserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PlatformRole { get; set; } = string.Empty;
        public string ActingRole { get; set; } = string.Empty;
        public string claimTypesRole { get; set; } = string.Empty;
        public List<Claim> allClaims { get; set; } = new();
        public bool Synced { get; set; }
        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value)
                    return;

                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInactive)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusLabel)));
            }
        }
        public List<string> AssignedStores { get; set; } = new();

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public int EffectiveOnlineUserId => OnlineUserId > 0 ? OnlineUserId : Id;
        public string DisplayName => Username;
        public string EmailDisplay => string.IsNullOrWhiteSpace(email) ? "No email configured" : email;
        public string EffectiveRole => string.IsNullOrWhiteSpace(ActingRole) ? PlatformRole : ActingRole;
        public string AssignedStoreSummary => AssignedStores.Count == 0 ? "No stores assigned" : string.Join(", ", AssignedStores);
        public bool IsInactive => !IsActive;
        public string Initials => string.Concat(
            (Username ?? string.Empty)
                .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0]))
        );
        public string StatusLabel => IsActive ? "Active" : "Inactive";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
