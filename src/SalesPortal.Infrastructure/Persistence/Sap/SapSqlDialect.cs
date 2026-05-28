using SalesPortal.Domain.Tenants;
using System;
using System.Linq;

namespace SalesPortal.Infrastructure.Persistence.Sap
{
    internal sealed class SapSqlDialect
    {
        private readonly Tenant _tenant;

        private SapSqlDialect(Tenant tenant)
        {
            _tenant = tenant;
        }

        public string NullValueFunction => _tenant.IsSqlServer ? "ISNULL" : "IFNULL";
        public string LimitOneClause => _tenant.IsSqlServer ? string.Empty : "\nLIMIT 1";
        public string TopOneClause => _tenant.IsSqlServer ? "TOP (1) " : string.Empty;
        public string TopClause(int rowCount) => _tenant.IsSqlServer ? $"TOP ({rowCount}) " : string.Empty;
        public string LimitClause(int rowCount) => _tenant.IsSqlServer ? string.Empty : $"\nLIMIT {rowCount}";

        public static SapSqlDialect For(Tenant tenant) => new(tenant);

        public string Parameter(int index) => _tenant.IsSqlServer ? $"@p{index}" : "?";

        public string Table(string tableName)
        {
            if (_tenant.IsSqlServer)
                return BuildSqlServerTableName(tableName);

            return $"{QuoteIdentifier(_tenant.Schema)}.{QuoteIdentifier(tableName)}";
        }

        public string Identifier(string identifier) => QuoteIdentifier(identifier);

        private string BuildSqlServerTableName(string tableName)
        {
            var schemaParts = _tenant.Schema
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!string.IsNullOrWhiteSpace(_tenant.Database) && schemaParts.Count == 1)
                schemaParts.Insert(0, _tenant.Database);

            if (schemaParts.Count == 0)
                schemaParts.Add("dbo");

            schemaParts.Add(tableName);

            return string.Join('.', schemaParts.Select(QuoteIdentifier));
        }

        private string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new InvalidOperationException("La configuración del tenant contiene un identificador de base de datos vacío.");

            return _tenant.IsSqlServer
                ? $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]"
                : $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}
