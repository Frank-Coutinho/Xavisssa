using System;

namespace Xavissa.Frontend.Models
{
    public class SaleHistoryQuery
    {
        public int? StoreId { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public string? SearchText { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; } = 100;
    }
}
