namespace SalesPortal.Domain.Tenants
{
    public sealed class TenantServiceLayerSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string CompanyDB { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int? DefaultSeries { get; set; }
        public string DefaultWarehouse { get; set; } = string.Empty;
        public bool AllowInvalidCertificate { get; set; }
    }
}
