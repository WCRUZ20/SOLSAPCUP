using SalesPortal.Application.Abstractions.Persistence;
using SalesPortal.Domain.Cup;
using SalesPortal.Domain.Tenants;
using System.Data;
using System.Data.Common;

namespace SalesPortal.Infrastructure.Persistence.Sap
{
    public sealed class SapCupRepository : ICupRepository
    {
        private readonly SapConnectionFactory _connectionFactory;

        public SapCupRepository(SapConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<Competitor>> GetCompetitorsAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT
                    {Column(dialect, "Code")},
                    {Column(dialect, "Name")},
                    {Column(dialect, "U_ID_PLAYER")},
                    {Column(dialect, "U_TEAM")},
                    {Column(dialect, "U_MATCHES")},
                    {Column(dialect, "U_POINTS")},
                    {Column(dialect, "U_DIFF_GOALS")},
                    {Column(dialect, "U_TB_POSITION")},
                    {Column(dialect, "U_ESTADO")}
                FROM {dialect.Table("@SS_COMPETITORS_CUP")}
                ORDER BY {dialect.NullValueFunction}({dialect.Identifier("U_TB_POSITION")}, 9999), {dialect.Identifier("Name")}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);
            await using var command = CreateCommand(connection, sql);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var competitors = new List<Competitor>();
            while (await reader.ReadAsync(cancellationToken))
                competitors.Add(MapCompetitor(reader));

            return competitors;
        }

        public async Task<Competitor?> GetCompetitorByPlayerIdAsync(Tenant tenant, string playerId, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                SELECT {dialect.TopOneClause}
                    {Column(dialect, "Code")},
                    {Column(dialect, "Name")},
                    {Column(dialect, "U_ID_PLAYER")},
                    {Column(dialect, "U_TEAM")},
                    {Column(dialect, "U_MATCHES")},
                    {Column(dialect, "U_POINTS")},
                    {Column(dialect, "U_DIFF_GOALS")},
                    {Column(dialect, "U_TB_POSITION")},
                    {Column(dialect, "U_ESTADO")}
                FROM {dialect.Table("@SS_COMPETITORS_CUP")}
                WHERE {dialect.Identifier("U_ID_PLAYER")} = {dialect.Parameter(0)}{dialect.LimitOneClause}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);
            await using var command = CreateCommand(connection, sql);
            AddParameter(command, dialect.Parameter(0), playerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            return await reader.ReadAsync(cancellationToken) ? MapCompetitor(reader) : null;
        }

        public async Task<IReadOnlyList<CupMatch>> GetMatchesAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = BuildMatchSelect(dialect) + $"\nORDER BY {dialect.Identifier("U_MATCH_DATE")}, {dialect.Identifier("U_MATCH_TIME")}";
            return await QueryMatchesAsync(tenant, sql, null, cancellationToken);
        }

        public async Task<IReadOnlyList<WorldCupCountry>> GetWorldCupCountriesAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var countryNameOrder = tenant.IsSqlServer
                ? $"CONVERT(nvarchar(4000), {dialect.Identifier("U_NOMB_PAIS")})"
                : dialect.Identifier("U_NOMB_PAIS");
            var sql = $@"
                SELECT
                    {Column(dialect, "Code")},
                    {Column(dialect, "Name")},
                    {Column(dialect, "U_COD_PAIS")},
                    {Column(dialect, "U_NOMB_PAIS")}
                FROM {dialect.Table("@PAISES_MUNDIAL")}
                ORDER BY {Column(dialect, "Code")}, {countryNameOrder}, {dialect.Identifier("Name")}";

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);
            await using var command = CreateCommand(connection, sql);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var countries = new List<WorldCupCountry>();
            while (await reader.ReadAsync(cancellationToken))
                countries.Add(MapWorldCupCountry(reader));

            return countries;
        }

        public async Task<IReadOnlyList<CupMatch>> GetMatchesForPlayerAsync(Tenant tenant, string playerId, string competitorCode, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = BuildMatchSelect(dialect) + $@"
                WHERE {dialect.Identifier("U_PLAYER1")} IN ({dialect.Parameter(0)}, {dialect.Parameter(1)})
                   OR {dialect.Identifier("U_PLAYER2")} IN ({dialect.Parameter(2)}, {dialect.Parameter(3)})
                ORDER BY {dialect.Identifier("U_MATCH_DATE")}, {dialect.Identifier("U_MATCH_TIME")}";

            return await QueryMatchesAsync(tenant, sql, command =>
            {
                AddParameter(command, dialect.Parameter(0), playerId);
                AddParameter(command, dialect.Parameter(1), competitorCode);
                AddParameter(command, dialect.Parameter(2), playerId);
                AddParameter(command, dialect.Parameter(3), competitorCode);
            }, cancellationToken);
        }

        public async Task SaveCompetitorAsync(Tenant tenant, Competitor competitor, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                UPDATE {dialect.Table("@SS_COMPETITORS_CUP")}
                SET {dialect.Identifier("Name")} = {dialect.Parameter(1)},
                    {dialect.Identifier("U_ID_PLAYER")} = {dialect.Parameter(2)},
                    {dialect.Identifier("U_TEAM")} = {dialect.Parameter(3)},
                    {dialect.Identifier("U_MATCHES")} = {dialect.Parameter(4)},
                    {dialect.Identifier("U_POINTS")} = {dialect.Parameter(5)},
                    {dialect.Identifier("U_DIFF_GOALS")} = {dialect.Parameter(6)},
                    {dialect.Identifier("U_TB_POSITION")} = {dialect.Parameter(7)},
                    {dialect.Identifier("U_ESTADO")} = {dialect.Parameter(8)}
                WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}

                IF @@ROWCOUNT = 0
                INSERT INTO {dialect.Table("@SS_COMPETITORS_CUP")} ({dialect.Identifier("Code")}, {dialect.Identifier("Name")}, {dialect.Identifier("U_ID_PLAYER")}, {dialect.Identifier("U_TEAM")}, {dialect.Identifier("U_MATCHES")}, {dialect.Identifier("U_POINTS")}, {dialect.Identifier("U_DIFF_GOALS")}, {dialect.Identifier("U_TB_POSITION")}, {dialect.Identifier("U_ESTADO")})
                VALUES ({dialect.Parameter(0)}, {dialect.Parameter(1)}, {dialect.Parameter(2)}, {dialect.Parameter(3)}, {dialect.Parameter(4)}, {dialect.Parameter(5)}, {dialect.Parameter(6)}, {dialect.Parameter(7)}, {dialect.Parameter(8)})";

            await ExecuteSaveAsync(tenant, sql, command => AddCompetitorParameters(command, dialect, competitor), cancellationToken);
        }

        public async Task SaveWorldCupCountryAsync(Tenant tenant, WorldCupCountry country, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                IF {dialect.Parameter(0)} IS NOT NULL AND EXISTS (SELECT 1 FROM {dialect.Table("@PAISES_MUNDIAL")} WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)})
                BEGIN
                    UPDATE {dialect.Table("@PAISES_MUNDIAL")}
                    SET {dialect.Identifier("Name")} = {dialect.Parameter(1)},
                        {dialect.Identifier("U_COD_PAIS")} = {dialect.Parameter(2)},
                        {dialect.Identifier("U_NOMB_PAIS")} = {dialect.Parameter(3)}
                    WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}
                END
                ELSE
                BEGIN
                    INSERT INTO {dialect.Table("@PAISES_MUNDIAL")} ({dialect.Identifier("Name")}, {dialect.Identifier("U_COD_PAIS")}, {dialect.Identifier("U_NOMB_PAIS")})
                    VALUES ({dialect.Parameter(1)}, {dialect.Parameter(2)}, {dialect.Parameter(3)})
                END";

            await ExecuteSaveAsync(tenant, sql, command => AddWorldCupCountryParameters(command, dialect, country), cancellationToken);
        }

        public async Task SaveMatchAsync(Tenant tenant, CupMatch match, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                UPDATE {dialect.Table("@SS_MATCHES")}
                SET {dialect.Identifier("Name")} = {dialect.Parameter(1)},
                    {dialect.Identifier("U_MATCH_DATE")} = {dialect.Parameter(2)},
                    {dialect.Identifier("U_MATCH_TIME")} = {dialect.Parameter(3)},
                    {dialect.Identifier("U_PLAYER1")} = {dialect.Parameter(4)},
                    {dialect.Identifier("U_PLAYER2")} = {dialect.Parameter(5)},
                    {dialect.Identifier("U_GOAL_P1")} = {dialect.Parameter(6)},
                    {dialect.Identifier("U_GOAL_P2")} = {dialect.Parameter(7)},
                    {dialect.Identifier("U_OBSERVATION")} = {dialect.Parameter(8)}
                WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}

                IF @@ROWCOUNT = 0
                INSERT INTO {dialect.Table("@SS_MATCHES")} ({dialect.Identifier("Code")}, {dialect.Identifier("Name")}, {dialect.Identifier("U_MATCH_DATE")}, {dialect.Identifier("U_MATCH_TIME")}, {dialect.Identifier("U_PLAYER1")}, {dialect.Identifier("U_PLAYER2")}, {dialect.Identifier("U_GOAL_P1")}, {dialect.Identifier("U_GOAL_P2")}, {dialect.Identifier("U_OBSERVATION")})
                VALUES ({dialect.Parameter(0)}, {dialect.Parameter(1)}, {dialect.Parameter(2)}, {dialect.Parameter(3)}, {dialect.Parameter(4)}, {dialect.Parameter(5)}, {dialect.Parameter(6)}, {dialect.Parameter(7)}, {dialect.Parameter(8)})";

            await ExecuteSaveAsync(tenant, sql, command => AddMatchParameters(command, dialect, match), cancellationToken);
        }


        public async Task DeleteCompetitorAsync(Tenant tenant, string code, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                DELETE FROM {dialect.Table("@SS_COMPETITORS_CUP")}
                WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}";

            await ExecuteSaveAsync(tenant, sql, command => AddParameter(command, dialect.Parameter(0), code), cancellationToken);
        }

        public async Task DeleteMatchAsync(Tenant tenant, string code, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                DELETE FROM {dialect.Table("@SS_MATCHES")}
                WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}";

            await ExecuteSaveAsync(tenant, sql, command => AddParameter(command, dialect.Parameter(0), code), cancellationToken);
        }

        public async Task DeleteWorldCupCountryAsync(Tenant tenant, int code, CancellationToken cancellationToken)
        {
            var dialect = SapSqlDialect.For(tenant);
            var sql = $@"
                DELETE FROM {dialect.Table("@PAISES_MUNDIAL")}
                WHERE {dialect.Identifier("Code")} = {dialect.Parameter(0)}";

            await ExecuteSaveAsync(tenant, sql, command => AddParameter(command, dialect.Parameter(0), code), cancellationToken);
        }

        private async Task<IReadOnlyList<CupMatch>> QueryMatchesAsync(Tenant tenant, string sql, Action<DbCommand>? configure, CancellationToken cancellationToken)
        {
            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);
            await using var command = CreateCommand(connection, sql);
            configure?.Invoke(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var matches = new List<CupMatch>();
            while (await reader.ReadAsync(cancellationToken))
                matches.Add(MapMatch(reader));

            return matches;
        }

        private async Task ExecuteSaveAsync(Tenant tenant, string sql, Action<DbCommand> configure, CancellationToken cancellationToken)
        {
            if (!tenant.IsSqlServer)
                throw new NotSupportedException("El mantenimiento administrativo está disponible para SQL Server.");

            await using var connection = _connectionFactory.CreateConnection(tenant);
            await connection.OpenAsync(cancellationToken);
            await using var command = CreateCommand(connection, sql);
            configure(command);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static string BuildMatchSelect(SapSqlDialect dialect) => $@"
                SELECT
                    {Column(dialect, "Code")},
                    {Column(dialect, "Name")},
                    {Column(dialect, "U_MATCH_DATE")},
                    {Column(dialect, "U_MATCH_TIME")},
                    {Column(dialect, "U_PLAYER1")},
                    {Column(dialect, "U_PLAYER2")},
                    {Column(dialect, "U_GOAL_P1")},
                    {Column(dialect, "U_GOAL_P2")},
                    {Column(dialect, "U_OBSERVATION")}
                FROM {dialect.Table("@SS_MATCHES")}";

        private static string Column(SapSqlDialect dialect, string name) => dialect.Identifier(name);
        private static DbCommand CreateCommand(DbConnection connection, string sql) { var command = connection.CreateCommand(); command.CommandText = sql; return command; }
        private static void AddParameter(DbCommand command, string name, object? value) { var p = command.CreateParameter(); p.ParameterName = name; p.Value = value ?? DBNull.Value; command.Parameters.Add(p); }
        private static void AddCompetitorParameters(DbCommand command, SapSqlDialect dialect, Competitor c) { AddParameter(command, dialect.Parameter(0), c.Code); AddParameter(command, dialect.Parameter(1), c.Name); AddParameter(command, dialect.Parameter(2), c.PlayerId); AddParameter(command, dialect.Parameter(3), c.Team); AddParameter(command, dialect.Parameter(4), c.Matches); AddParameter(command, dialect.Parameter(5), c.Points); AddParameter(command, dialect.Parameter(6), c.GoalDifference); AddParameter(command, dialect.Parameter(7), c.TablePosition); AddParameter(command, dialect.Parameter(8), c.Status); }
        private static void AddMatchParameters(DbCommand command, SapSqlDialect dialect, CupMatch m) { AddParameter(command, dialect.Parameter(0), m.Code); AddParameter(command, dialect.Parameter(1), m.Name); AddParameter(command, dialect.Parameter(2), m.MatchDate); AddParameter(command, dialect.Parameter(3), m.MatchTime); AddParameter(command, dialect.Parameter(4), m.Player1); AddParameter(command, dialect.Parameter(5), m.Player2); AddParameter(command, dialect.Parameter(6), m.GoalsPlayer1); AddParameter(command, dialect.Parameter(7), m.GoalsPlayer2); AddParameter(command, dialect.Parameter(8), m.Observation); }
        private static void AddWorldCupCountryParameters(DbCommand command, SapSqlDialect dialect, WorldCupCountry c) { AddParameter(command, dialect.Parameter(0), c.Code); AddParameter(command, dialect.Parameter(1), c.Name); AddParameter(command, dialect.Parameter(2), c.CountryCode); AddParameter(command, dialect.Parameter(3), c.CountryName); }
        private static Competitor MapCompetitor(IDataRecord r) => new() { Code = r["Code"]?.ToString() ?? string.Empty, Name = r["Name"]?.ToString() ?? string.Empty, PlayerId = r["U_ID_PLAYER"]?.ToString() ?? string.Empty, Team = r["U_TEAM"]?.ToString() ?? string.Empty, Matches = ToDecimal(r["U_MATCHES"]), Points = ToDecimal(r["U_POINTS"]), GoalDifference = ToDecimal(r["U_DIFF_GOALS"]), TablePosition = ToDecimal(r["U_TB_POSITION"]), Status = r["U_ESTADO"]?.ToString() ?? string.Empty };
        private static CupMatch MapMatch(IDataRecord r) => new() { Code = r["Code"]?.ToString() ?? string.Empty, Name = r["Name"]?.ToString() ?? string.Empty, MatchDate = r["U_MATCH_DATE"] == DBNull.Value ? null : Convert.ToDateTime(r["U_MATCH_DATE"]), MatchTime = r["U_MATCH_TIME"] == DBNull.Value ? null : Convert.ToInt16(r["U_MATCH_TIME"]), Player1 = r["U_PLAYER1"]?.ToString() ?? string.Empty, Player2 = r["U_PLAYER2"]?.ToString() ?? string.Empty, GoalsPlayer1 = ToNullableDecimal(r["U_GOAL_P1"]), GoalsPlayer2 = ToNullableDecimal(r["U_GOAL_P2"]), Observation = r["U_OBSERVATION"]?.ToString() ?? string.Empty };
        private static WorldCupCountry MapWorldCupCountry(IDataRecord r) => new() { Code = r["Code"] == DBNull.Value ? null : Convert.ToInt32(r["Code"]), Name = r["Name"]?.ToString() ?? string.Empty, CountryCode = r["U_COD_PAIS"]?.ToString() ?? string.Empty, CountryName = r["U_NOMB_PAIS"]?.ToString() ?? string.Empty };
        private static decimal ToDecimal(object value) => value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        private static decimal? ToNullableDecimal(object value) => value == DBNull.Value ? null : Convert.ToDecimal(value);
    }
}
