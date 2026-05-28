using System;

namespace SalesPortal.Domain.Orders
{
    public sealed class CustomerOrder
    {
        public int DocEntry { get; set; }
        public int DocNum { get; set; }
        public DateTime DocDate { get; set; }
        public decimal DocTotal { get; set; }
        public string Comments { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
