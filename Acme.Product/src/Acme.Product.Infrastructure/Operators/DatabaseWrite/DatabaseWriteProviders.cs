using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace Acme.Product.Infrastructure.Operators.DatabaseWrite;

internal interface IDatabaseWriteProvider
{
    string DbType { get; }

    DbConnection CreateConnection(string connectionString);

    Task InitializeConnectionAsync(DbConnection connection, CancellationToken cancellationToken);

    DbCommand CreateEnsureTableCommand(DbConnection connection, string tableName);

    DbCommand CreateUpsertCommand(
        DbConnection connection,
        string tableName,
        string recordId,
        string dataJson,
        DateTime timestampUtc);

    bool IsTransient(Exception exception);
}

internal abstract class DatabaseWriteProviderBase : IDatabaseWriteProvider
{
    public abstract string DbType { get; }

    public abstract DbConnection CreateConnection(string connectionString);

    public virtual Task InitializeConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public abstract DbCommand CreateEnsureTableCommand(DbConnection connection, string tableName);

    public abstract DbCommand CreateUpsertCommand(
        DbConnection connection,
        string tableName,
        string recordId,
        string dataJson,
        DateTime timestampUtc);

    public virtual bool IsTransient(Exception exception)
    {
        return exception is TimeoutException;
    }

    protected static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

internal sealed class SqliteDatabaseWriteProvider : DatabaseWriteProviderBase
{
    public override string DbType => "SQLite";

    public override DbConnection CreateConnection(string connectionString)
    {
        return new SqliteConnection(connectionString);
    }

    public override async Task InitializeConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
        await pragmaCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public override DbCommand CreateEnsureTableCommand(DbConnection connection, string tableName)
    {
        var command = connection.CreateCommand();
        var identifier = QuoteIdentifier(tableName);
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {identifier} (
                Id TEXT PRIMARY KEY,
                Data TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );
            """;
        return command;
    }

    public override DbCommand CreateUpsertCommand(
        DbConnection connection,
        string tableName,
        string recordId,
        string dataJson,
        DateTime timestampUtc)
    {
        var command = connection.CreateCommand();
        var identifier = QuoteIdentifier(tableName);
        command.CommandText = $"""
            INSERT INTO {identifier} (Id, Data, Timestamp)
            VALUES (@Id, @Data, @Timestamp)
            ON CONFLICT(Id) DO UPDATE SET
              Data = excluded.Data,
              Timestamp = excluded.Timestamp;
            """;
        AddParameter(command, "@Id", recordId);
        AddParameter(command, "@Data", dataJson);
        AddParameter(command, "@Timestamp", timestampUtc.ToString("O"));
        return command;
    }

    public override bool IsTransient(Exception exception)
    {
        if (exception is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode is 5 or 6;
        }

        return base.IsTransient(exception);
    }

    private static string QuoteIdentifier(string tableName)
    {
        return "\"" + tableName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

internal sealed class SqlServerDatabaseWriteProvider : DatabaseWriteProviderBase
{
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        -2,
        53,
        4060,
        10928,
        10929,
        1205,
        2601,
        2627,
        40197,
        40501,
        40613
    ];

    public override string DbType => "SQLServer";

    public override DbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }

    public override DbCommand CreateEnsureTableCommand(DbConnection connection, string tableName)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[{tableName}] (
                    [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
                    [Data] NVARCHAR(MAX) NOT NULL,
                    [Timestamp] DATETIME2 NOT NULL
                );
            END;
            """;
        return command;
    }

    public override DbCommand CreateUpsertCommand(
        DbConnection connection,
        string tableName,
        string recordId,
        string dataJson,
        DateTime timestampUtc)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            MERGE [dbo].[{tableName}] WITH (HOLDLOCK) AS target
            USING (VALUES (@Id, @Data, @Timestamp)) AS source ([Id], [Data], [Timestamp])
              ON target.[Id] = source.[Id]
            WHEN MATCHED THEN
              UPDATE SET [Data] = source.[Data], [Timestamp] = source.[Timestamp]
            WHEN NOT MATCHED THEN
              INSERT ([Id], [Data], [Timestamp]) VALUES (source.[Id], source.[Data], source.[Timestamp]);
            """;
        AddParameter(command, "@Id", recordId);
        AddParameter(command, "@Data", dataJson);
        AddParameter(command, "@Timestamp", timestampUtc);
        return command;
    }

    public override bool IsTransient(Exception exception)
    {
        if (exception is SqlException sqlException)
        {
            return sqlException.Errors.Cast<SqlError>().Any(error => TransientErrorNumbers.Contains(error.Number));
        }

        return base.IsTransient(exception);
    }
}

internal sealed class MySqlDatabaseWriteProvider : DatabaseWriteProviderBase
{
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        1042,
        1047,
        1129,
        1130,
        1205,
        1213,
        2002,
        2006,
        2013
    ];

    public override string DbType => "MySQL";

    public override DbConnection CreateConnection(string connectionString)
    {
        return new MySqlConnection(connectionString);
    }

    public override DbCommand CreateEnsureTableCommand(DbConnection connection, string tableName)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS `{tableName}` (
                `Id` VARCHAR(128) NOT NULL,
                `Data` LONGTEXT NOT NULL,
                `Timestamp` DATETIME(6) NOT NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE = InnoDB;
            """;
        return command;
    }

    public override DbCommand CreateUpsertCommand(
        DbConnection connection,
        string tableName,
        string recordId,
        string dataJson,
        DateTime timestampUtc)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO `{tableName}` (`Id`, `Data`, `Timestamp`)
            VALUES (@Id, @Data, @Timestamp)
            ON DUPLICATE KEY UPDATE
              `Data` = VALUES(`Data`),
              `Timestamp` = VALUES(`Timestamp`);
            """;
        AddParameter(command, "@Id", recordId);
        AddParameter(command, "@Data", dataJson);
        AddParameter(command, "@Timestamp", timestampUtc);
        return command;
    }

    public override bool IsTransient(Exception exception)
    {
        if (exception is MySqlException mySqlException)
        {
            return TransientErrorNumbers.Contains(mySqlException.Number);
        }

        return base.IsTransient(exception);
    }
}
