using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Orders;
using SalesPortal.Domain.Tenants;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SalesPortal.Infrastructure.Persistence.Sap
{
    public sealed class SapOrderRepository : IOrderRepository
    {
        private readonly SapConnectionFactory _connectionFactory;

        public SapOrderRepository(SapConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<CustomerOrder>> GetCustomerOrdersAsync(
            Tenant tenant,
            string cardCode,
            DateTime? dateFrom,
            DateTime? dateTo,
            int? docNum,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = new StringBuilder($@"
                SELECT
                    {Column(dialect, "T0", "DocEntry")},
                    {Column(dialect, "T0", "DocNum")},
                    {Column(dialect, "T0", "DocDate")},
                    {Column(dialect, "T0", "DocTotal")},
                    {Column(dialect, "T0", "Comments")},
                    {Column(dialect, "T0", "Address2")},
                   
                    {Column(dialect, "T1", "Name")} {dialect.Identifier("estado")}
                FROM {dialect.Table("ORDR")} T0
                LEFT JOIN {dialect.Table("@SS_STATUS")} T1 ON {Column(dialect, "T1", "Code")} = {Column(dialect, "T0", "U_Status")}
                WHERE {Column(dialect, "T0", "CardCode")} = {dialect.Parameter(0)}
                AND {Column(dialect, "T0", "U_FromWeb")} = 'Y'
                ");

            var parameterIndex = 1;

            if (dateFrom.HasValue)
            {
                sql.AppendLine($" AND {Column(dialect, "T0", "DocDate")} >= {dialect.Parameter(parameterIndex)}");
                parameterIndex++;
            }

            if (dateTo.HasValue)
            {
                sql.AppendLine($" AND {Column(dialect, "T0", "DocDate")} <= {dialect.Parameter(parameterIndex)}");
                parameterIndex++;
            }

            if (docNum.HasValue)
            {
                sql.AppendLine($" AND {Column(dialect, "T0", "DocNum")} LIKE '%{dialect.Parameter(parameterIndex)}%'");
            }

            sql.AppendLine($" ORDER BY {Column(dialect, "T0", "DocDate")} DESC, {Column(dialect, "T0", "DocNum")} DESC");

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql.ToString());
            AddParameter(command, dialect.Parameter(0), cardCode);

            parameterIndex = 1;

            if (dateFrom.HasValue)
            {
                AddParameter(command, dialect.Parameter(parameterIndex), dateFrom.Value.Date);
                parameterIndex++;
            }

            if (dateTo.HasValue)
            {
                AddParameter(command, dialect.Parameter(parameterIndex), dateTo.Value.Date);
                parameterIndex++;
            }

            if (docNum.HasValue)
            {
                AddParameter(command, dialect.Parameter(parameterIndex), docNum.Value);
            }

            var orders = new List<CustomerOrder>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(MapOrder(reader));
            }

            return orders;
        }

        public async Task<CustomerOrderDetail?> GetCustomerOrderDetailAsync(
            Tenant tenant,
            string cardCode,
            int docEntry,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var headerSql = $@"
                SELECT
                    {Column(dialect, "T0", "DocEntry")},
                    {Column(dialect, "T0", "DocNum")},
                    {Column(dialect, "T0", "DocDate")},
                    {Column(dialect, "T0", "DocTotal")},
                    {Column(dialect, "T0", "Comments")},
                    {Column(dialect, "T0", "Address2")},
                    {Column(dialect, "T1", "Name")} {dialect.Identifier("estado")}
                FROM {dialect.Table("ORDR")} T0
                LEFT JOIN {dialect.Table("@SS_STATUS")} T1 ON {Column(dialect, "T1", "Code")} = {Column(dialect, "T0", "U_Status")}
                WHERE {Column(dialect, "T0", "CardCode")} = {dialect.Parameter(0)}
                    AND {Column(dialect, "T0", "DocEntry")} = {dialect.Parameter(1)}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            CustomerOrderDetail? order;

            await using (var headerCommand = CreateCommand(connection, headerSql))
            {
                AddParameter(headerCommand, dialect.Parameter(0), cardCode);
                AddParameter(headerCommand, dialect.Parameter(1), docEntry);

                await using var reader = await headerCommand.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                order = MapOrderDetail(reader);
            }

            var linesSql = $@"
                SELECT
                    {Column(dialect, "T0", "ItemCode")},
                    {Column(dialect, "T0", "Dscription")},
                    {Column(dialect, "T0", "Quantity")},
                    {Column(dialect, "T0", "Price")},
                    {dialect.NullValueFunction}({Column(dialect, "T0", "DiscPrcnt")}, 0) AS {dialect.Identifier("DiscountPercent")},
                    {Column(dialect, "T0", "LineTotal")}
                FROM {dialect.Table("RDR1")} T0
                WHERE {Column(dialect, "T0", "DocEntry")} = {dialect.Parameter(0)}
                ORDER BY {Column(dialect, "T0", "LineNum")}";

            await using (var linesCommand = CreateCommand(connection, linesSql))
            {
                AddParameter(linesCommand, dialect.Parameter(0), docEntry);

                var lines = new List<CustomerOrderLine>();
                await using var reader = await linesCommand.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    lines.Add(MapOrderLine(reader));
                }

                order.Lines = lines;
            }

            return order;
        }

        public async Task<IReadOnlyList<SalesItem>> GetSalesItemsAsync(
            Tenant tenant,
            string cardCode,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT
                    {Column(dialect, "T0", "ItemCode")},
                    {Column(dialect, "T0", "FrgnName")},
                    {Column(dialect, "T0", "ItemName")},
                    {dialect.NullValueFunction}({Column(dialect, "T3", "ItmsGrpNam")}, '') AS {dialect.Identifier("CategoryName")},
                    {dialect.NullValueFunction}({Column(dialect, "T1", "Price")}, 0) AS {dialect.Identifier("UnitPrice")},
                    {dialect.NullValueFunction}({Column(dialect, "T2", "Discount")}, 0) AS {dialect.Identifier("DiscountPercent")},
                    {dialect.NullValueFunction}({Column(dialect, "T0", "VatGourpSa")}, '') AS {dialect.Identifier("TaxCode")}
                FROM {dialect.Table("OITM")} T0
                INNER JOIN {dialect.Table("OCRD")} T2
                    ON {Column(dialect, "T2", "CardCode")} = {dialect.Parameter(0)}
                LEFT JOIN {dialect.Table("ITM1")} T1
                    ON {Column(dialect, "T1", "ItemCode")} = {Column(dialect, "T0", "ItemCode")}
                    AND {Column(dialect, "T1", "PriceList")} = {Column(dialect, "T2", "ListNum")}
                LEFT JOIN {dialect.Table("OITB")} T3
                    ON {Column(dialect, "T3", "ItmsGrpCod")} = {Column(dialect, "T0", "ItmsGrpCod")}
                WHERE {Column(dialect, "T0", "SellItem")} = 'Y'
                    AND {Column(dialect, "T0", "validFor")} = 'Y'
                ORDER BY {Column(dialect, "T0", "ItemCode")}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), cardCode);

            var items = new List<SalesItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapSalesItem(reader));
            }

            return items;
        }

        public async Task<IReadOnlyList<ShippingAddress>> GetShippingAddressesAsync(
            Tenant tenant,
            string cardCode,
            CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT
                    {dialect.Identifier("Address")},
                    {dialect.Identifier("Street")},
                    {dialect.Identifier("Block")},
                    {dialect.Identifier("City")},
                    {dialect.Identifier("County")},
                    {dialect.Identifier("State")},
                    {dialect.Identifier("ZipCode")},
                    {dialect.Identifier("Country")}
                FROM {dialect.Table("CRD1")}
                WHERE {dialect.Identifier("CardCode")} = {dialect.Parameter(0)}
                    AND {dialect.Identifier("AdresType")} = 'S'
                ORDER BY {dialect.Identifier("Address")}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), cardCode);

            var addresses = new List<ShippingAddress>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                addresses.Add(MapShippingAddress(reader));
            }

            return addresses;
        }

        private static string BuildStatusSubquery(SapSqlDialect dialect)
        {
            return $@"(
                SELECT {dialect.TopOneClause}{Column(dialect, "X0", "U_Estado")}
                FROM {dialect.Table("@SS_STATUS_ORDR_DET1")} X0
                WHERE {Column(dialect, "X0", "DocEntry")} = {Column(dialect, "T0", "U_LogStatus")}
                ORDER BY {Column(dialect, "X0", "LineId")} DESC{dialect.LimitOneClause})";
        }

        private static string Column(SapSqlDialect dialect, string alias, string columnName)
        {
            return $"{alias}.{dialect.Identifier(columnName)}";
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

        private static CustomerOrderDetail MapOrderDetail(IDataRecord reader)
        {
            return new CustomerOrderDetail
            {
                DocEntry = Convert.ToInt32(reader["DocEntry"], CultureInfo.InvariantCulture),
                DocNum = Convert.ToInt32(reader["DocNum"], CultureInfo.InvariantCulture),
                DocDate = Convert.ToDateTime(reader["DocDate"], CultureInfo.InvariantCulture),
                DocTotal = Convert.ToDecimal(reader["DocTotal"], CultureInfo.InvariantCulture),
                Comments = reader["Comments"]?.ToString() ?? string.Empty,
                ShippingAddress = reader["Address2"]?.ToString() ?? string.Empty,
                Status = reader["estado"]?.ToString() ?? string.Empty
            };
        }

        private static CustomerOrderLine MapOrderLine(IDataRecord reader)
        {
            return new CustomerOrderLine
            {
                ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                Description = reader["Dscription"]?.ToString() ?? string.Empty,
                Quantity = Convert.ToDecimal(reader["Quantity"], CultureInfo.InvariantCulture),
                UnitPrice = Convert.ToDecimal(reader["Price"], CultureInfo.InvariantCulture),
                DiscountPercent = Convert.ToDecimal(reader["DiscountPercent"], CultureInfo.InvariantCulture),
                LineTotal = Convert.ToDecimal(reader["LineTotal"], CultureInfo.InvariantCulture)
            };
        }

        private static SalesItem MapSalesItem(IDataRecord reader)
        {
            var itemName = reader["ItemName"]?.ToString() ?? string.Empty;
            var frgnName = reader["FrgnName"]?.ToString() ?? string.Empty;

            return new SalesItem
            {
                ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                FrgnName = string.IsNullOrWhiteSpace(frgnName) ? itemName : frgnName,
                CategoryName = reader["CategoryName"]?.ToString() ?? string.Empty,
                UnitPrice = Convert.ToDecimal(reader["UnitPrice"], CultureInfo.InvariantCulture),
                DiscountPercent = Convert.ToDecimal(reader["DiscountPercent"], CultureInfo.InvariantCulture),
                TaxCode = reader["TaxCode"]?.ToString() ?? string.Empty
            };
        }

        private static ShippingAddress MapShippingAddress(IDataRecord reader)
        {
            var parts = new[]
            {
                reader["Street"]?.ToString(),
                reader["Block"]?.ToString(),
                reader["City"]?.ToString(),
                reader["County"]?.ToString(),
                reader["State"]?.ToString(),
                reader["ZipCode"]?.ToString(),
                reader["Country"]?.ToString()
            };

            return new ShippingAddress
            {
                AddressName = reader["Address"]?.ToString() ?? string.Empty,
                FullAddress = string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)))
            };
        }

        private static CustomerOrder MapOrder(IDataRecord reader)
        {
            return new CustomerOrder
            {
                DocEntry = Convert.ToInt32(reader["DocEntry"], CultureInfo.InvariantCulture),
                DocNum = Convert.ToInt32(reader["DocNum"], CultureInfo.InvariantCulture),
                DocDate = Convert.ToDateTime(reader["DocDate"], CultureInfo.InvariantCulture),
                DocTotal = Convert.ToDecimal(reader["DocTotal"], CultureInfo.InvariantCulture),
                Comments = reader["Comments"]?.ToString() ?? string.Empty,
                ShippingAddress = reader["Address2"]?.ToString() ?? string.Empty,
                Status = reader["estado"]?.ToString() ?? string.Empty
            };
        }
    }
}
