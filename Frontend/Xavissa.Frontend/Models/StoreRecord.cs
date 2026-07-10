using System.Linq;

namespace Xavissa.Frontend.Models
{
    public class StoreRecord
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string Initials => string.Concat(
            (Name ?? string.Empty)
                .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0]))
        );
        public string CodeDisplay => string.IsNullOrWhiteSpace(Code) ? "Code pending" : Code;
        public string TenantDisplay => TenantId > 0 ? $"Tenant {TenantId}" : "Tenant not set";
        public string StatusLabel => IsActive ? "Active" : "Inactive";
        public string ToggleStatusLabel => IsActive ? "Set Inactive" : "Set Active";

        public override string ToString() => string.IsNullOrWhiteSpace(Code) ? Name : $"{Name} ({Code})";
    }
}
