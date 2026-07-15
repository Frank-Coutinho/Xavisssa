using System;
using System.ComponentModel.DataAnnotations;

namespace Xavissa.Frontend.Models
{
    public class SyncCursor
    {
        [Key]
        public string Key { get; set; } = string.Empty;
        public DateTime? Value { get; set; }
    }
}
