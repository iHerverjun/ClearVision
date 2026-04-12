using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Operators.DatabaseWrite;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "数据库写入",
    Description = "将输入数据写入 SQLite / SQL Server / MySQL 表。",
    Category = "数据",
    IconName = "database",
    Keywords = new[] { "数据库", "写入", "存储", "SQL", "SQLite", "SQLServer", "MySQL", "Upsert" })]
[InputPort("Data", "数据", PortDataType.Any, IsRequired = true)]
[InputPort("RecordId", "记录ID", PortDataType.String, IsRequired = false)]
[OutputPort("Status", "状态", PortDataType.Boolean)]
[OutputPort("RecordId", "记录ID", PortDataType.String)]
[OperatorParam("ConnectionString", "连接字符串", "string", DefaultValue = "")]
[OperatorParam("TableName", "表名", "string", DefaultValue = "InspectionResults")]
[OperatorParam("DbType", "数据库类型", "enum", DefaultValue = "SQLite", Options = new[] { "SQLite|SQLite", "SQLServer|SQLServer", "MySQL|MySQL" })]
public sealed class DatabaseWriteOperator : OperatorBase
{
    private const int CommandTimeoutSeconds = 5;
    private const int RetryAttempts = 3;
    private const int MaxRecordIdLength = 128;

    private static readonly Regex ValidTableNameRegex = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly ConcurrentDictionary<string, bool> TableExistsCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TableEnsureLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };
    private static readonly IReadOnlyDictionary<string, IDatabaseWriteProvider> ProviderByDbType =
        new Dictionary<string, IDatabaseWriteProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["SQLite"] = new SqliteDatabaseWriteProvider(),
            ["SQLServer"] = new SqlServerDatabaseWriteProvider(),
            ["MySQL"] = new MySqlDatabaseWriteProvider()
        };

    public override OperatorType OperatorType => OperatorType.DatabaseWrite;

    public DatabaseWriteOperator(ILogger<DatabaseWriteOperator> logger) : base(logger)
    {
    }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || !inputs.TryGetValue("Data", out var data) || data == null)
        {
            return OperatorExecutionOutput.Failure("Input 'Data' is required.");
        }

        var dbType = GetRawStringParameter(@operator, "DbType", "SQLite");
        if (!TryGetProvider(dbType, out var provider))
        {
            return OperatorExecutionOutput.Failure("DbType must be one of: SQLite, SQLServer, MySQL.");
        }

        var connectionString = GetRawStringParameter(@operator, "ConnectionString", string.Empty);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return OperatorExecutionOutput.Failure("ConnectionString cannot be empty.");
        }

        var tableName = GetRawStringParameter(@operator, "TableName", "InspectionResults");
        if (string.IsNullOrWhiteSpace(tableName))
        {
            tableName = "InspectionResults";
        }

        if (!IsValidTableName(tableName))
        {
            return OperatorExecutionOutput.Failure(
                $"TableName '{tableName}' is invalid. Only letters, digits and underscore are allowed, and it must start with a letter or underscore.");
        }

        var (recordIdSuccess, recordId, recordIdErrorMessage) = ResolveRecordId(inputs);
        if (!recordIdSuccess)
        {
            return OperatorExecutionOutput.Failure(recordIdErrorMessage ?? "Invalid RecordId.");
        }

        var dataJson = JsonSerializer.Serialize(data, SerializerOptions);
        var timestampUtc = DateTime.UtcNow;

        var writeResult = await WriteToDatabaseAsync(
            provider,
            connectionString,
            tableName,
            recordId,
            dataJson,
            timestampUtc,
            cancellationToken);

        if (!writeResult.success)
        {
            return OperatorExecutionOutput.Failure($"Database write failed: {writeResult.errorMessage}");
        }

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Status"] = true,
            ["RecordId"] = recordId,
            ["TableName"] = tableName,
            ["DbType"] = provider.DbType,
            ["Timestamp"] = timestampUtc
        });
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var dbType = GetRawStringParameter(@operator, "DbType", "SQLite");
        if (!TryGetProvider(dbType, out _))
        {
            return ValidationResult.Invalid("DbType must be one of: SQLite, SQLServer, MySQL.");
        }

        var connectionString = GetRawStringParameter(@operator, "ConnectionString", string.Empty);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidationResult.Invalid("ConnectionString cannot be empty.");
        }

        var tableName = GetRawStringParameter(@operator, "TableName", "InspectionResults");
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return ValidationResult.Invalid("TableName cannot be empty.");
        }

        if (!IsValidTableName(tableName))
        {
            return ValidationResult.Invalid(
                $"TableName '{tableName}' is invalid. Only letters, digits and underscore are allowed, and it must start with a letter or underscore.");
        }

        return ValidationResult.Valid();
    }

    private static bool IsValidTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        return ValidTableNameRegex.IsMatch(tableName);
    }

    private static bool TryGetProvider(string dbType, out IDatabaseWriteProvider provider)
    {
        if (ProviderByDbType.TryGetValue(dbType, out var resolvedProvider))
        {
            provider = resolvedProvider;
            return true;
        }

        provider = null!;
        return false;
    }

    private static string GetRawStringParameter(Operator @operator, string name, string defaultValue)
    {
        var parameter = @operator.Parameters
            .FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (parameter == null)
        {
            return defaultValue;
        }

        if (!string.IsNullOrWhiteSpace(parameter.ValueJson))
        {
            try
            {
                return JsonSerializer.Deserialize<string>(parameter.ValueJson) ?? defaultValue;
            }
            catch (JsonException)
            {
                // Fall through to generic object formatting below.
            }
        }

        return parameter.Value?.ToString()?.Trim() ?? defaultValue;
    }

    private static (bool success, string recordId, string? errorMessage) ResolveRecordId(Dictionary<string, object> inputs)
    {
        if (!inputs.TryGetValue("RecordId", out var recordIdValue) || recordIdValue == null)
        {
            return (true, Guid.NewGuid().ToString("N"), null);
        }

        var candidate = Convert.ToString(recordIdValue)?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return (false, string.Empty, "Input 'RecordId' cannot be empty when provided.");
        }

        if (candidate.Length > MaxRecordIdLength)
        {
            return (false, string.Empty, $"Input 'RecordId' exceeds {MaxRecordIdLength} characters.");
        }

        return (true, candidate, null);
    }

    private async Task<(bool success, string? errorMessage)> WriteToDatabaseAsync(
        IDatabaseWriteProvider provider,
        string connectionString,
        string tableName,
        string recordId,
        string dataJson,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= RetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var connection = provider.CreateConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await provider.InitializeConnectionAsync(connection, cancellationToken);

                await EnsureTableExistsAsync(provider, connection, connectionString, tableName, cancellationToken);
                await ExecuteUpsertAsync(provider, connection, tableName, recordId, dataJson, timestampUtc, cancellationToken);

                Logger.LogInformation(
                    "[DatabaseWrite] Record {RecordId} was persisted to {DbType}.{TableName}.",
                    recordId,
                    provider.DbType,
                    tableName);

                return (true, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < RetryAttempts && provider.IsTransient(ex))
            {
                var delay = TimeSpan.FromMilliseconds(200 * attempt);
                Logger.LogWarning(
                    ex,
                    "[DatabaseWrite] Transient {DbType} failure on attempt {Attempt}/{RetryAttempts}, retrying after {DelayMs} ms.",
                    provider.DbType,
                    attempt,
                    RetryAttempts,
                    delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "[DatabaseWrite] Failed to persist record {RecordId} into {DbType}.{TableName}.",
                    recordId,
                    provider.DbType,
                    tableName);
                return (false, ex.Message);
            }
        }

        return (false, "Database write failed after all retry attempts.");
    }

    private static async Task ExecuteUpsertAsync(
        IDatabaseWriteProvider provider,
        DbConnection connection,
        string tableName,
        string recordId,
        string dataJson,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        await using var upsertCommand = provider.CreateUpsertCommand(connection, tableName, recordId, dataJson, timestampUtc);
        upsertCommand.CommandTimeout = CommandTimeoutSeconds;
        await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureTableExistsAsync(
        IDatabaseWriteProvider provider,
        DbConnection connection,
        string connectionString,
        string tableName,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildTableCacheKey(provider.DbType, connectionString, tableName);
        if (TableExistsCache.ContainsKey(cacheKey))
        {
            return;
        }

        var guard = TableEnsureLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await guard.WaitAsync(cancellationToken);

        try
        {
            if (TableExistsCache.ContainsKey(cacheKey))
            {
                return;
            }

            await using var ensureTableCommand = provider.CreateEnsureTableCommand(connection, tableName);
            ensureTableCommand.CommandTimeout = CommandTimeoutSeconds;
            await ensureTableCommand.ExecuteNonQueryAsync(cancellationToken);
            TableExistsCache.TryAdd(cacheKey, true);
        }
        finally
        {
            guard.Release();
            if (TableExistsCache.ContainsKey(cacheKey))
            {
                TableEnsureLocks.TryRemove(cacheKey, out _);
            }
        }
    }

    private static string BuildTableCacheKey(string dbType, string connectionString, string tableName)
    {
        var hash = ComputeConnectionHash(connectionString);
        return $"{dbType}|{hash}|{tableName}";
    }

    private static string ComputeConnectionHash(string connectionString)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(connectionString));
        return Convert.ToHexString(bytes);
    }
}
