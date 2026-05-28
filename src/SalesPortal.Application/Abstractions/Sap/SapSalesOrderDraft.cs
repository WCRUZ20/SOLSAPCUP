using System;
using System.Collections.Generic;

namespace SalesPortal.Application.Abstractions.Sap
{
    public sealed class SapSalesOrderDraft
    {
        public string CardCode { get; set; } = string.Empty;
        public string ShippingAddressName { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public IReadOnlyList<SapSalesOrderDraftLine> Lines { get; set; } = Array.Empty<SapSalesOrderDraftLine>();
    }

    public sealed class SapSalesOrderDraftLine
    {
        public string ItemCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal DiscountPercent { get; set; }
        public string? TaxCode { get; set; }
    }
}
