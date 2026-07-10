using System;

namespace Xavissa.Frontend.Models
{
    public class SyncLog
    {
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // "Insert", "Update", etc.
        public string Payload { get; set; } = string.Empty;
        public bool Synced { get; set; } = false;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
