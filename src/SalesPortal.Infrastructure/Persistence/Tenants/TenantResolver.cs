using Microsoft.Extensions.Configuration;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Tenants;
using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Infrastructure.Persistence.Tenants
{
    public sealed class TenantResolver : ITenantResolver
    {
        private readonly IConfiguration _configuration;

        public TenantResolver(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Tenant ResolveByHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("No se pudo resolver el host del portal.");

            var tenants = _configuration
                .GetSection("Tenants")
                .Get<List<TenantConfig>>() ?? new List<TenantConfig>();

            var cleanHost = host.Split(':')[0].Trim().ToLower();
            //var cleanHost = host;

            var tenant = tenants.FirstOrDefault(x =>
                x.Host.Trim().ToLower() == cleanHost ||
                cleanHost.StartsWith(x.Code.Trim().ToLower()));

            if (tenant == null)
                throw new InvalidOperationException($"No existe configuración para el host: {host}");

            return new Tenant
            {
                Code = tenant.Code,
                Host = tenant.Host,
                DbType = tenant.DbType,
                Database = tenant.Database,
                Schema = tenant.Schema,
                ConnectionString = tenant.ConnectionString,
                ServiceLayer = new TenantServiceLayerSettings
                {
                    BaseUrl = tenant.ServiceLayer.BaseUrl,
                    CompanyDB = tenant.ServiceLayer.CompanyDB,
                    UserName = tenant.ServiceLayer.UserName,
                    Password = tenant.ServiceLayer.Password,
                    DefaultSeries = tenant.ServiceLayer.DefaultSeries,
                    DefaultWarehouse = tenant.ServiceLayer.DefaultWarehouse,
                    AllowInvalidCertificate = tenant.ServiceLayer.AllowInvalidCertificate
                }
            };
        }
    }
}
