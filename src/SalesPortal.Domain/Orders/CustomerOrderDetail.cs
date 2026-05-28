using System;
using System.Collections.Generic;

namespace SalesPortal.Domain.Orders
{
    public sealed class CustomerOrderDetail
    {
        public int DocEntry { get; set; }
        public int DocNum { get; set; }
        public DateTime DocDate { get; set; }
        public decimal DocTotal { get; set; }
        public string Comments { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public IReadOnlyList<CustomerOrderLine> Lines { get; set; } = Array.Empty<CustomerOrderLine>();
    }
}
