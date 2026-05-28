using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Domain.Tenants
{
    public sealed class Tenant
    {
        public string Code { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string DbType { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public TenantServiceLayerSettings ServiceLayer { get; set; } = new();

        public bool IsHana => DbType.Equals("HANA", StringComparison.OrdinalIgnoreCase);
        public bool IsSqlServer => DbType.Equals("SQLSERVER", StringComparison.OrdinalIgnoreCase);
    }
}
