using Microsoft.Extensions.DependencyInjection;
using SalesPortal.Application.Abstractions.Authentication;
using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Application.Abstractions.Security;
using SalesPortal.Application.Auth.Services;
using SalesPortal.Infrastructure.Persistence.Sap;
using SalesPortal.Infrastructure.Persistence.Tenants;
using SalesPortal.Infrastructure.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SalesPortal.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<ITenantResolver, TenantResolver>();
            services.AddScoped<SapConnectionFactory>();
            services.AddScoped<ICustomerRepository, SapCustomerRepository>();
            services.AddScoped<ICupRepository, SapCupRepository>();
            services.AddScoped<INoticeRepository, SapNoticeRepository>();

            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
