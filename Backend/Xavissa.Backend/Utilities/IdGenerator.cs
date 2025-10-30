using System;

namespace Xavissa.Backend.Utilities
{
    public static class IdGenerator
    {
        public static string GenerateId(string prefix)
        {
            // Example: "PROD-20251029-AB12CD"
            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            string randomPart = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            return $"{prefix}-{datePart}-{randomPart}";
        }
    }
}
