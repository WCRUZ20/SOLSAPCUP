using Microsoft.Data.SqlClient;
using SalesPortal.Domain.Tenants;
using Sap.Data.Hana;
using System;
using System.Data.Common;

namespace SalesPortal.Infrastructure.Persistence.Sap
{
    public sealed class SapConnectionFactory
    {
        public DbConnection CreateConnection(Tenant tenant)
        {
            if (tenant.IsHana)
                return new HanaConnection(tenant.ConnectionString);

            if (tenant.IsSqlServer)
                return new SqlConnection(tenant.ConnectionString);

            throw new NotSupportedException($"Motor de base de datos no soportado: {tenant.DbType}");
        }
    }
}
