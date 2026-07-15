using System;
using System.Collections.Generic;

namespace Xavissa.Frontend.Models
{
    public class TenantAnalyticsSummary
    {
        public int TenantId { get; set; }
        public int StoreCount { get; set; }
        public int ProductCount { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageSaleValue { get; set; }
        public List<StoreAnalyticsSummary> Stores { get; set; } = new();
    }

    public class StoreAnalyticsSummary
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageSaleValue { get; set; }
        public DateTime? LastSaleDate { get; set; }
    }

    public class StoreAnalyticsResponse
    {
        public int StoreId { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageSaleValue { get; set; }
    }
}
