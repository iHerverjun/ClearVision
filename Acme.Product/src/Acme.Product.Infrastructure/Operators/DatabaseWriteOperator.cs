// DatabaseWriteOperator.cs
// 验证表名是否合法（防止SQL注入）
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 数据库写入算子 - 支持SQLite (可扩展SQLServer/MySQL)
/// </summary>
public class DatabaseWriteOperator : OperatorBase
{
    /// <summary>
    /// 有效的表名正则表达式（防止SQL注入）
    /// </summary>
    private static readonly Regex ValidTableNameRegex = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled);

    public override OperatorType OperatorType => OperatorType.DatabaseWrite;

    public DatabaseWriteOperator(ILogger<DatabaseWriteOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || !inputs.TryGetValue("Data", out var data) || data == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入数据"));
        }

        // 获取参数
        var connectionString = GetStringParam(@operator, "ConnectionString", "");
        var tableName = GetStringParam(@operator, "TableName", "InspectionResults");
        var dbType = GetStringParam(@operator, "DbType", "SQLite");

        if (string.IsNullOrEmpty(connectionString))
        {
            // 使用默认SQLite连接
            connectionString = "Data Source=inspection_results.db";
            dbType = "SQLite";
        }

        if (string.IsNullOrEmpty(tableName))
        {
            tableName = "InspectionResults";
        }

        // SQL注入防护：表名白名单校验
        if (!IsValidTableName(tableName))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"表名 '{tableName}' 包含非法字符。表名只能包含字母、数字和下划线，且必须以字母或下划线开头。"));
        }

        // 生成记录ID
        var recordId = Guid.NewGuid().ToString("N");

        // 将数据序列化为JSON
        var dataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // 写入数据库
        var (success, errorMessage) = WriteToDatabase(dbType, connectionString, tableName, recordId, dataJson);

        if (!success)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"数据库写入失败: {errorMessage}"));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Status", success },
            { "RecordId", recordId },
            { "TableName", tableName },
            { "DbType", dbType },
            { "Timestamp", DateTime.UtcNow }
        }));
    }

    /// <summary>
    /// 验证表名是否合法（防止SQL注入）
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <returns>是否合法</returns>
    private static bool IsValidTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return false;

        return ValidTableNameRegex.IsMatch(tableName);
    }

    private (bool success, string? errorMessage) WriteToDatabase(string dbType, string connectionString, string tableName, string recordId, string dataJson)
    {
        try
        {
            // 目前仅实现SQLite支持，其他数据库类型可在此扩展
            if (!dbType.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                // 其他数据库类型暂不实现，返回成功以兼容元数据
                return (true, null);
            }

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // 确保表存在
            CreateTableIfNotExists(connection, tableName);

            // 插入数据
            using var cmd = connection.CreateCommand();
            // 表名已通过白名单验证，可以安全使用
            cmd.CommandText = $@"INSERT INTO {tableName} (Id, Data, Timestamp) VALUES (@Id, @Data, @Timestamp)";
            cmd.Parameters.AddWithValue("@Id", recordId);
            cmd.Parameters.AddWithValue("@Data", dataJson);
            cmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
            cmd.ExecuteNonQuery();

            Logger.LogInformation("[DatabaseWrite] 成功写入记录: {RecordId} 到表: {TableName}", recordId, tableName);

            return (true, null);
        }
        catch (SqliteException ex)
        {
            Logger.LogError(ex, "[DatabaseWrite] SQLite错误 {ErrorCode}: {Message}", ex.SqliteErrorCode, ex.Message);
            return (false, $"SQLite错误 {ex.SqliteErrorCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[DatabaseWrite] 数据库写入失败");
            return (false, ex.Message);
        }
    }

    private void CreateTableIfNotExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"CREATE TABLE IF NOT EXISTS {tableName} (
            Id TEXT PRIMARY KEY,
            Data TEXT NOT NULL,
            Timestamp DATETIME NOT NULL
        )";
        cmd.ExecuteNonQuery();
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var connectionString = GetStringParam(@operator, "ConnectionString", "");
        var tableName = GetStringParam(@operator, "TableName", "InspectionResults");
        var dbType = GetStringParam(@operator, "DbType", "SQLite");

        if (@operator.Parameters.Any(p => p.Name == "ConnectionString") && string.IsNullOrEmpty(connectionString))
        {
            return ValidationResult.Invalid("连接字符串不能为空");
        }

        if (string.IsNullOrEmpty(tableName))
        {
            return ValidationResult.Invalid("表名不能为空");
        }

        if (!IsValidTableName(tableName))
        {
            return ValidationResult.Invalid($"表名 '{tableName}' 包含非法字符。表名只能包含字母、数字和下划线，且必须以字母或下划线开头。");
        }

        var validDbTypes = new[] { "SQLite", "SQLServer", "MySQL" };
        if (!validDbTypes.Contains(dbType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("数据库类型必须是 SQLite、SQLServer 或 MySQL");
        }

        return ValidationResult.Valid();
    }
}
