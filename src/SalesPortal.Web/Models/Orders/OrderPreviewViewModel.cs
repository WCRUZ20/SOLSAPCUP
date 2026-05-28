using System;
using System.Collections.Generic;

namespace SalesPortal.Web.Models.Orders
{
    public sealed class OrderPreviewViewModel
    {
        public int DocEntry { get; set; }
        public int DocNum { get; set; }
        public DateTime DocDate { get; set; }
        public decimal DocTotal { get; set; }
        public string Comments { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public IReadOnlyList<OrderPreviewLineViewModel> Lines { get; set; } = Array.Empty<OrderPreviewLineViewModel>();
    }
}
