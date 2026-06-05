using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Notices;
using SalesPortal.Domain.Tenants;
using System.Data;
using System.Data.Common;

namespace SalesPortal.Infrastructure.Persistence.Sap
{
    public sealed class SapNoticeRepository : INoticeRepository
    {
        private const string NoticeTableName = "@SS_LOGIN_NOTICES";
        private readonly SapConnectionFactory _connectionFactory;

        public SapNoticeRepository(SapConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<LoginNotice>> GetNoticesAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = BuildNoticeSelect(dialect) + $"\nORDER BY {dialect.Identifier("U_START_DATE")} DESC, {dialect.Identifier("Code")}";
            return await QueryNoticesAsync(tenant, sql, null, cancellationToken);
        }

        public async Task<IReadOnlyList<LoginNotice>> GetActiveLoginNoticesAsync(Tenant tenant, DateTime currentDate, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = BuildNoticeSelect(dialect) + $@"
                WHERE {dialect.Identifier("U_ACTIVE")} = {dialect.Parameter(0)}
                  AND ({dialect.Identifier("U_START_DATE")} IS NULL OR {dialect.Identifier("U_START_DATE")} <= {dialect.Parameter(1)})
                  AND ({dialect.Identifier("U_END_DATE")} IS NULL OR {dialect.Identifier("U_END_DATE")} >= {dialect.Parameter(2)})
                ORDER BY {dialect.Identifier("U_START_DATE")} DESC, {dialect.Identifier("Code")}";

            return await QueryNoticesAsync(tenant, sql, command =>
            {
                AddParameter(command, dialect.Parameter(0), "Y");
                AddParameter(command, dialect.Parameter(1), currentDate.Date);
                AddParameter(command, dialect.Parameter(2), currentDate.Date);
            }, cancellationToken);
        }

        public async Task SaveNoticeAsync(Tenant tenant, LoginNotice notice, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                UPDATE {dialect.Table(NoticeTableName)}
                SET {dialect.Identifier("Name")} = {dialect.Parameter(1)},
                    {dialect.Identifier("U_TITLE")} = {dialect.Parameter(2)},
                    {dialect.Identifier("U_MESSAGE")} = {dialect.Parameter(3)},
                    {dialect.Identifier("U_IMAGE_URL")} = {dialect.Parameter(8)},
                    {dialect.Identifier("U_START_DATE")} = {dialect.Parameter(4)},
                    {dialect.Identifier("U_END_DATE")} = {dialect.Parameter(5)},
                    {dialect.Identifier("U_DURATION_SECONDS")} = {dialect.Parameter(6)},
                    {dialect.Identifier("U_ACTIVE")} = {dialect.Parameter(7)}
                WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}

                IF @@ROWCOUNT = 0
                INSERT INTO {dialect.Table(NoticeTableName)} ({dialect.Identifier("Code")}, {dialect.Identifier("Name")}, {dialect.Identifier("U_TITLE")}, {dialect.Identifier("U_MESSAGE")}, {dialect.Identifier("U_IMAGE_URL")}, {dialect.Identifier("U_START_DATE")}, {dialect.Identifier("U_END_DATE")}, {dialect.Identifier("U_DURATION_SECONDS")}, {dialect.Identifier("U_ACTIVE")})
                VALUES ({dialect.Parameter(0)}, {dialect.Parameter(1)}, {dialect.Parameter(2)}, {dialect.Parameter(3)}, {dialect.Parameter(8)}, {dialect.Parameter(4)}, {dialect.Parameter(5)}, {dialect.Parameter(6)}, {dialect.Parameter(7)})";

            await ExecuteSaveAsync(tenant, sql, command => AddNoticeParameters(command, dialect, notice), cancellationToken);
        }

        public async Task DeleteNoticeAsync(Tenant tenant, string code, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                DELETE FROM {dialect.Table(NoticeTableName)}
                WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}";

            await ExecuteSaveAsync(tenant, sql, command => AddParameter(command, dialect.Parameter(0), code), cancellationToken);
        }

        private async Task<IReadOnlyList<LoginNotice>> QueryNoticesAsync(Tenant tenant, string sql, Action<DbCommand>? configure, CancellationToken cancellationToken)
        {
            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);
            if (tenant.IsSqlServer)
                await EnsureNoticeTableAsync(connection, SapSqlDialect.For(tenant), cancellationToken);
            await using var command = CreateCommand(connection, sql);
            configure?.Invoke(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var notices = new List<LoginNotice>();
            while (await reader.ReadAsync(cancellationToken))
                notices.Add(MapNotice(reader));

            return notices;
        }

        private async Task ExecuteSaveAsync(Tenant tenant, string sql, Action<DbCommand> configure, CancellationToken cancellationToken)
        {
            if (!tenant.IsSqlServer)
                throw new NotSupportedException("El mantenimiento administrativo de avisos está disponible para SQL Server.");

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);
            await EnsureNoticeTableAsync(connection, SapSqlDialect.For(tenant), cancellationToken);
            await using var command = CreateCommand(connection, sql);
            configure(command);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task EnsureNoticeTableAsync(DbConnection connection, SapSqlDialect dialect, CancellationToken cancellationToken)
        {
            var sql = $@"
                IF OBJECT_ID(N'{dialect.Table(NoticeTableName).Replace("'", "''", StringComparison.Ordinal)}', N'U') IS NULL
                BEGIN
                    CREATE TABLE {dialect.Table(NoticeTableName)} (
                        {dialect.Identifier("Code")} nvarchar(50) NOT NULL PRIMARY KEY,
                        {dialect.Identifier("Name")} nvarchar(100) NOT NULL,
                        {dialect.Identifier("U_TITLE")} nvarchar(150) NOT NULL,
                        {dialect.Identifier("U_MESSAGE")} nvarchar(max) NOT NULL,
                        {dialect.Identifier("U_IMAGE_URL")} nvarchar(500) NULL,
                        {dialect.Identifier("U_START_DATE")} date NULL,
                        {dialect.Identifier("U_END_DATE")} date NULL,
                        {dialect.Identifier("U_DURATION_SECONDS")} int NOT NULL CONSTRAINT DF_SS_LOGIN_NOTICES_DURATION DEFAULT (8),
                        {dialect.Identifier("U_ACTIVE")} char(1) NOT NULL CONSTRAINT DF_SS_LOGIN_NOTICES_ACTIVE DEFAULT ('Y')
                    )
                END

                IF COL_LENGTH(N'{dialect.Table(NoticeTableName).Replace("'", "''", StringComparison.Ordinal)}', N'U_IMAGE_URL') IS NULL
                BEGIN
                    ALTER TABLE {dialect.Table(NoticeTableName)} ADD {dialect.Identifier("U_IMAGE_URL")} nvarchar(500) NULL
                END";

            await using var command = CreateCommand(connection, sql);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static string BuildNoticeSelect(SapSqlDialect dialect) => $@"
                SELECT
                    {Column(dialect, "Code")},
                    {Column(dialect, "Name")},
                    {Column(dialect, "U_TITLE")},
                    {Column(dialect, "U_MESSAGE")},
                    {Column(dialect, "U_IMAGE_URL")},
                    {Column(dialect, "U_START_DATE")},
                    {Column(dialect, "U_END_DATE")},
                    {Column(dialect, "U_DURATION_SECONDS")},
                    {Column(dialect, "U_ACTIVE")}
                FROM {dialect.Table(NoticeTableName)}";

        private static string Column(SapSqlDialect dialect, string name) => dialect.Identifier(name);
        private static DbCommand CreateCommand(DbConnection connection, string sql) { var command = connection.CreateCommand(); command.CommandText = sql; return command; }
        private static void AddParameter(DbCommand command, string name, object? value) { var p = command.CreateParameter(); p.ParameterName = name; p.Value = value ?? DBNull.Value; command.Parameters.Add(p); }
        private static void AddNoticeParameters(DbCommand command, SapSqlDialect dialect, LoginNotice notice) { AddParameter(command, dialect.Parameter(0), notice.Code); AddParameter(command, dialect.Parameter(1), notice.Title); AddParameter(command, dialect.Parameter(2), notice.Title); AddParameter(command, dialect.Parameter(3), notice.Message); AddParameter(command, dialect.Parameter(4), notice.StartDate?.Date); AddParameter(command, dialect.Parameter(5), notice.EndDate?.Date); AddParameter(command, dialect.Parameter(6), notice.DurationSeconds); AddParameter(command, dialect.Parameter(7), notice.IsActive ? "Y" : "N"); AddParameter(command, dialect.Parameter(8), string.IsNullOrWhiteSpace(notice.ImageUrl) ? null : notice.ImageUrl.Trim()); }
        private static LoginNotice MapNotice(IDataRecord r) => new() { Code = r["Code"]?.ToString() ?? string.Empty, Title = r["U_TITLE"]?.ToString() ?? r["Name"]?.ToString() ?? string.Empty, Message = r["U_MESSAGE"]?.ToString() ?? string.Empty, ImageUrl = r["U_IMAGE_URL"]?.ToString() ?? string.Empty, StartDate = r["U_START_DATE"] == DBNull.Value ? null : Convert.ToDateTime(r["U_START_DATE"]), EndDate = r["U_END_DATE"] == DBNull.Value ? null : Convert.ToDateTime(r["U_END_DATE"]), DurationSeconds = r["U_DURATION_SECONDS"] == DBNull.Value ? 8 : Convert.ToInt32(r["U_DURATION_SECONDS"]), IsActive = string.Equals(r["U_ACTIVE"]?.ToString(), "Y", StringComparison.OrdinalIgnoreCase) };
    }
}
