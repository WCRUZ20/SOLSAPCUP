using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Infrastructure.Persistence.Tenants
{
    public sealed class TenantOptions
    {
        public List<TenantConfig> Tenants { get; set; } = new();
    }

    public sealed class TenantConfig
    {
        public string Code { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public TenantServiceLayerConfig ServiceLayer { get; set; } = new();
    }

    public sealed class TenantServiceLayerConfig
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
