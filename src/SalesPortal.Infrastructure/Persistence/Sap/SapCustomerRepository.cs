using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Customers;
using SalesPortal.Domain.Tenants;
using SalesPortal.Shared.Constants;
using System;
using System.Data;
using System.Data.Common;

namespace SalesPortal.Infrastructure.Persistence.Sap
{
    public sealed class SapCustomerRepository : ICustomerRepository
    {
        private readonly SapConnectionFactory _connectionFactory;

        public SapCustomerRepository(SapConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<CustomerPortalUser?> GetByUserOrEmailAsync(
            Tenant tenant,
            string userOrEmail,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT {dialect.TopOneClause}
                    {dialect.Identifier("CardCode")},
                    {dialect.Identifier("CardName")},
                    {dialect.Identifier(SapUserFields.UserWeb)} AS {dialect.Identifier("UserWeb")},
                    {dialect.Identifier(SapUserFields.EmailWeb)} AS {dialect.Identifier("EmailWeb")},
                    {dialect.Identifier(SapUserFields.PwdWeb)} AS {dialect.Identifier("PasswordWeb")},
                    {dialect.Identifier(SapUserFields.ChangePwd)} AS {dialect.Identifier("ChangePwd")},
                    {dialect.Identifier("validFor")}
                FROM {dialect.Table("OCRD")}
                WHERE 
                    {dialect.Identifier("CardType")} = 'C'
                    AND {dialect.Identifier("validFor")} = 'Y'
                    AND (
                        LOWER({dialect.NullValueFunction}({dialect.Identifier(SapUserFields.UserWeb)}, '')) = LOWER({dialect.Parameter(0)})
                        OR LOWER({dialect.NullValueFunction}({dialect.Identifier(SapUserFields.EmailWeb)}, '')) = LOWER({dialect.Parameter(1)})
                    ){dialect.LimitOneClause}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), userOrEmail);
            AddParameter(command, dialect.Parameter(1), userOrEmail);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapCustomer(reader);
        }

        public async Task<CustomerPortalUser?> GetByCardCodeAsync(
            Tenant tenant,
            string cardCode,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT {dialect.TopOneClause}
                    {dialect.Identifier("CardCode")},
                    {dialect.Identifier("CardName")},
                    {dialect.Identifier(SapUserFields.UserWeb)} AS {dialect.Identifier("UserWeb")},
                    {dialect.Identifier(SapUserFields.EmailWeb)} AS {dialect.Identifier("EmailWeb")},
                    {dialect.Identifier(SapUserFields.PwdWeb)} AS {dialect.Identifier("PasswordWeb")},
                    {dialect.Identifier(SapUserFields.ChangePwd)} AS {dialect.Identifier("ChangePwd")},
                    {dialect.Identifier("validFor")}
                FROM {dialect.Table("OCRD")}
                WHERE 
                    {dialect.Identifier("CardType")} = 'C'
                    AND {dialect.Identifier("CardCode")} = {dialect.Parameter(0)}{dialect.LimitOneClause}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), cardCode);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapCustomer(reader);
        }

        public async Task UpdatePasswordAsync(
            Tenant tenant,
            string cardCode,
            string newPassword,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                UPDATE {dialect.Table("OCRD")}
                SET 
                    {dialect.Identifier(SapUserFields.PwdWeb)} = {dialect.Parameter(0)},
                    {dialect.Identifier(SapUserFields.ChangePwd)} = 'N'
                WHERE {dialect.Identifier("CardCode")} = {dialect.Parameter(1)}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), newPassword);
            AddParameter(command, dialect.Parameter(1), cardCode);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static DbCommand CreateCommand(DbConnection connection, string sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static CustomerPortalUser MapCustomer(IDataRecord reader)
        {
            return new CustomerPortalUser
            {
                CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                CardName = reader["CardName"]?.ToString() ?? string.Empty,
                UserWeb = reader["UserWeb"]?.ToString() ?? string.Empty,
                EmailWeb = reader["EmailWeb"]?.ToString() ?? string.Empty,
                PasswordWeb = reader["PasswordWeb"]?.ToString() ?? string.Empty,
                MustChangePassword = (reader["ChangePwd"]?.ToString() ?? "N") == "Y",
                IsActive = (reader["validFor"]?.ToString() ?? "N") == "Y"
            };
        }
    }
}
