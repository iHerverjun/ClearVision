using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using NSubstitute;

namespace Acme.Product.Tests.Integration;

[Trait("Category", "DatabaseIntegration")]
public sealed class DatabaseWriteOperatorMultiDbIntegrationTests : IAsyncLifetime
{
    private const string SqlServerPassword = "StrongPassw0rd!";
    private const string MySqlPassword = "StrongPassw0rd!";
    private readonly DatabaseWriteOperator _operator;
    private readonly DockerDatabaseContainer _sqlServerContainer;
    private readonly DockerDatabaseContainer _mySqlContainer;
    private bool _dockerAvailable;
    private bool _sqlServerAvailable;
    private bool _mySqlAvailable;

    public DatabaseWriteOperatorMultiDbIntegrationTests()
    {
        _operator = new DatabaseWriteOperator(Substitute.For<ILogger<DatabaseWriteOperator>>());
        _sqlServerContainer = DockerDatabaseContainer.CreateSqlServer(SqlServerPassword);
        _mySqlContainer = DockerDatabaseContainer.CreateMariaDb(MySqlPassword);
    }

    public async Task InitializeAsync()
    {
        _dockerAvailable = await DockerDatabaseContainer.IsDockerAvailableAsync();
        if (!_dockerAvailable)
        {
            return;
        }

        _sqlServerAvailable = await DockerDatabaseContainer.IsImageAvailableAsync(_sqlServerContainer.Image);
        _mySqlAvailable = await DockerDatabaseContainer.IsImageAvailableAsync(_mySqlContainer.Image);

        if (_sqlServerAvailable)
        {
            await _sqlServerContainer.StartAsync();
        }

        if (_mySqlAvailable)
        {
            await _mySqlContainer.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerAvailable)
        {
            await _sqlServerContainer.DisposeAsync();
        }

        if (_mySqlAvailable)
        {
            await _mySqlContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithProvidedRecordId_ShouldUpsertSingleRowInSqlServer()
    {
        EnsureContainerAvailable(_sqlServerAvailable, "SQLServer");

        var tableName = $"Inspection{Guid.NewGuid():N}"[..20];
        var op = CreateOperator(_sqlServerContainer.ConnectionString, tableName, "SQLServer");
        const string recordId = "sqlserver-record";

        var firstResult = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Data"] = new { Value = 1 },
            ["RecordId"] = recordId
        });

        var secondResult = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Data"] = new { Value = 2 },
            ["RecordId"] = recordId
        });

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.OutputData!["DbType"].Should().Be("SQLServer");

        await using var connection = new SqlConnection(_sqlServerContainer.ConnectionString);
        await connection.OpenAsync();

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM [dbo].[{tableName}] WHERE [Id] = @Id";
        countCommand.Parameters.AddWithValue("@Id", recordId);
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        count.Should().Be(1);

        await using var dataCommand = connection.CreateCommand();
        dataCommand.CommandText = $"SELECT [Data] FROM [dbo].[{tableName}] WHERE [Id] = @Id";
        dataCommand.Parameters.AddWithValue("@Id", recordId);
        var data = (string?)await dataCommand.ExecuteScalarAsync();
        data.Should().NotBeNullOrWhiteSpace();
        using var json = JsonDocument.Parse(data!);
        json.RootElement.GetProperty("Value").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithConcurrentSameRecordId_ShouldRemainSingleRowInSqlServer()
    {
        EnsureContainerAvailable(_sqlServerAvailable, "SQLServer");

        var tableName = $"Inspection{Guid.NewGuid():N}"[..20];
        var op = CreateOperator(_sqlServerContainer.ConnectionString, tableName, "SQLServer");
        const string recordId = "sqlserver-concurrent";

        var tasks = Enumerable.Range(1, 4)
            .Select(index => _operator.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Data"] = new { Value = index },
                ["RecordId"] = recordId
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        results.Should().OnlyContain(result => result.IsSuccess);

        await using var connection = new SqlConnection(_sqlServerContainer.ConnectionString);
        await connection.OpenAsync();

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM [dbo].[{tableName}] WHERE [Id] = @Id";
        countCommand.Parameters.AddWithValue("@Id", recordId);
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithProvidedRecordId_ShouldUpsertSingleRowInMariaDb()
    {
        EnsureContainerAvailable(_mySqlAvailable, "MySQL");

        var tableName = $"inspection_{Guid.NewGuid():N}"[..20];
        var op = CreateOperator(_mySqlContainer.ConnectionString, tableName, "MySQL");
        const string recordId = "mysql-record";

        var firstResult = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Data"] = new { Value = 1 },
            ["RecordId"] = recordId
        });

        var secondResult = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Data"] = new { Value = 2 },
            ["RecordId"] = recordId
        });

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.OutputData!["DbType"].Should().Be("MySQL");

        await using var connection = new MySqlConnection(_mySqlContainer.ConnectionString);
        await connection.OpenAsync();

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM `{tableName}` WHERE `Id` = @Id";
        countCommand.Parameters.AddWithValue("@Id", recordId);
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        count.Should().Be(1);

        await using var dataCommand = connection.CreateCommand();
        dataCommand.CommandText = $"SELECT `Data` FROM `{tableName}` WHERE `Id` = @Id";
        dataCommand.Parameters.AddWithValue("@Id", recordId);
        var data = (string?)await dataCommand.ExecuteScalarAsync();
        data.Should().NotBeNullOrWhiteSpace();
        using var json = JsonDocument.Parse(data!);
        json.RootElement.GetProperty("Value").GetInt32().Should().Be(2);
    }

    private void EnsureContainerAvailable(bool isAvailable, string dbType)
    {
        if (!_dockerAvailable || !isAvailable)
        {
            throw new InvalidOperationException($"Docker or required {dbType} image is not available for DatabaseWrite integration tests.");
        }
    }

    private static Operator CreateOperator(
        string connectionString,
        string tableName,
        string dbType)
    {
        var op = new Operator("DatabaseWriteIntegration", OperatorType.DatabaseWrite, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(),
            "ConnectionString",
            "Connection String",
            "Database connection string",
            "string",
            connectionString,
            isRequired: true));

        op.AddParameter(new Parameter(
            Guid.NewGuid(),
            "TableName",
            "Table Name",
            "Target table name",
            "string",
            tableName,
            isRequired: true));

        op.AddParameter(new Parameter(
            Guid.NewGuid(),
            "DbType",
            "Database Type",
            "Supported database type",
            "enum",
            dbType,
            isRequired: true,
            options: new List<ParameterOption>
            {
                new() { Label = "SQLite", Value = "SQLite" },
                new() { Label = "SQLServer", Value = "SQLServer" },
                new() { Label = "MySQL", Value = "MySQL" }
            }));

        return op;
    }

    private sealed class DockerDatabaseContainer : IAsyncDisposable
    {
        private readonly string _image;
        private readonly int _containerPort;
        private readonly IReadOnlyDictionary<string, string> _environment;
        private readonly Func<int, string> _connectionStringFactory;
        private readonly string _name;

        private DockerDatabaseContainer(
            string image,
            int containerPort,
            IReadOnlyDictionary<string, string> environment,
            Func<int, string> connectionStringFactory)
        {
            _image = image;
            _containerPort = containerPort;
            _environment = environment;
            _connectionStringFactory = connectionStringFactory;
            _name = $"cv-db-{Guid.NewGuid():N}";
            HostPort = FindFreePort();
        }

        public int HostPort { get; }

        public string Image => _image;

        public string ConnectionString => _connectionStringFactory(HostPort);

        public static DockerDatabaseContainer CreateSqlServer(string password)
        {
            return new DockerDatabaseContainer(
                "mcr.microsoft.com/mssql/server:2022-latest",
                1433,
                new Dictionary<string, string>
                {
                    ["ACCEPT_EULA"] = "Y",
                    ["SA_PASSWORD"] = password,
                    ["MSSQL_PID"] = "Developer"
                },
                port => $"Server=127.0.0.1,{port};User ID=sa;Password={password};TrustServerCertificate=True;Encrypt=False;");
        }

        public static DockerDatabaseContainer CreateMariaDb(string password)
        {
            return new DockerDatabaseContainer(
                "mariadb:11.4",
                3306,
                new Dictionary<string, string>
                {
                    ["MARIADB_ROOT_PASSWORD"] = password,
                    ["MARIADB_DATABASE"] = "clearvision"
                },
                port => $"Server=127.0.0.1;Port={port};User ID=root;Password={password};Database=clearvision;");
        }

        public async Task StartAsync()
        {
            await RunDockerCommandAsync(BuildRunArguments());
            await WaitUntilReadyAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await RunDockerCommandAsync(new[] { "rm", "-f", _name });
            }
            catch
            {
            }
        }

        public static async Task<bool> IsDockerAvailableAsync()
        {
            try
            {
                var exitCode = await RunProcessAsync("docker", new[] { "info", "--format", "{{.ServerVersion}}" }, TimeSpan.FromSeconds(10));
                return exitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> IsImageAvailableAsync(string image)
        {
            try
            {
                var exitCode = await RunProcessAsync("docker", new[] { "image", "inspect", image }, TimeSpan.FromSeconds(10));
                return exitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private string[] BuildRunArguments()
        {
            var arguments = new List<string> { "run", "-d", "--rm", "--name", _name };
            foreach (var pair in _environment)
            {
                arguments.Add("-e");
                arguments.Add($"{pair.Key}={pair.Value}");
            }

            arguments.Add("-p");
            arguments.Add($"{HostPort}:{_containerPort}");
            arguments.Add(_image);
            return arguments.ToArray();
        }

        private async Task WaitUntilReadyAsync()
        {
            var timeout = DateTime.UtcNow.AddSeconds(90);
            Exception? lastException = null;

            while (DateTime.UtcNow < timeout)
            {
                try
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync();
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            throw new TimeoutException($"Container {_name} did not become ready in time.", lastException);
        }

        private DbConnection CreateConnection()
        {
            if (_containerPort == 1433)
            {
                return new SqlConnection(ConnectionString);
            }

            return new MySqlConnection(ConnectionString);
        }

        private static int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunDockerCommandAsync(string[] arguments)
        {
            var exitCode = await RunProcessAsync("docker", arguments, TimeSpan.FromMinutes(10));
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"docker {string.Join(" ", arguments)} failed with exit code {exitCode}.");
            }
        }

        private static async Task<int> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var waitForExitTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(waitForExitTask, Task.Delay(timeout));
            if (completedTask != waitForExitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                throw new TimeoutException($"{fileName} {string.Join(" ", arguments)} timed out after {timeout}.");
            }

            return process.ExitCode;
        }
    }
}
