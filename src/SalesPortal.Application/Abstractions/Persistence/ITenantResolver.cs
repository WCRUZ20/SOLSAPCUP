using SalesPortal.Domain.Tenants;

namespace SalesPortal.Application.Abstractions.Persistence;

public interface ITenantResolver
{
    Tenant ResolveByHost(string host);
}