namespace SalesPortal.Domain.Orders
{
    public sealed class SalesItem
    {
        public string ItemCode { get; set; } = string.Empty;
        public string FrgnName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public string TaxCode { get; set; } = string.Empty;
    }
}
