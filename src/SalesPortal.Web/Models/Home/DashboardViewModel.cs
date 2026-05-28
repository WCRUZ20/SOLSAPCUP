using System;
using System.Collections.Generic;

namespace SalesPortal.Web.Models.Home
{
    public sealed class DashboardViewModel
    {
        public int TotalOrders { get; set; }
        public int OrdersLast30Days { get; set; }
        public decimal TotalAmountLast30Days { get; set; }
        public decimal AverageTicketLast30Days { get; set; }
        public int AvailableProducts { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public IReadOnlyList<DashboardMonthAmountViewModel> MonthlySales { get; set; } = Array.Empty<DashboardMonthAmountViewModel>();
        public IReadOnlyList<DashboardStatusSummaryViewModel> OrdersByStatus { get; set; } = Array.Empty<DashboardStatusSummaryViewModel>();
        public IReadOnlyList<DashboardRecentOrderViewModel> RecentOrders { get; set; } = Array.Empty<DashboardRecentOrderViewModel>();
    }

    public sealed class DashboardMonthAmountViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Percentage { get; set; }
    }

    public sealed class DashboardStatusSummaryViewModel
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Percentage { get; set; }
    }

    public sealed class DashboardRecentOrderViewModel
    {
        public int DocEntry { get; set; }
        public int DocNum { get; set; }
        public DateTime DocDate { get; set; }
        public decimal DocTotal { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
    }
}
