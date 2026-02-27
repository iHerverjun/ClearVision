using Acme.PlcComm;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Acme.Product.Desktop.Endpoints;

/// <summary>
/// PLC settings APIs.
/// </summary>
public static class PlcEndpoints
{
    public static IEndpointRouteBuilder MapPlcEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/plc/test-connection", async (
            PlcTestConnectionRequest request,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var ipAddress = (request.IpAddress ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return Results.BadRequest(new { success = false, message = "IP address is required." });
            }

            if (request.Port <= 0 || request.Port > 65535)
            {
                return Results.BadRequest(new { success = false, message = "Port must be between 1 and 65535." });
            }

            try
            {
                if (TryBuildPlcCommConnectionString(request, out var connectionString, out var protocol))
                {
                    var logger = loggerFactory.CreateLogger("PlcConnectionTest");
                    using var client = PlcClientFactory.CreateFromConnectionString(connectionString, logger);

                    var connected = await client.ConnectAsync(ct);
                    var pingOk = connected && await client.PingAsync(ct);

                    if (connected)
                    {
                        await SafeDisconnectAsync(client);
                    }

                    return Results.Ok(new
                    {
                        success = pingOk,
                        message = pingOk ? "Connection succeeded." : "Connection failed.",
                        protocol
                    });
                }

                // For protocols not supported by Acme.PlcComm yet (e.g. ModbusTcp/CIP),
                // provide a lightweight TCP reachability test.
                var tcpReachable = await TestTcpReachabilityAsync(ipAddress, request.Port, ct);
                return Results.Ok(new
                {
                    success = tcpReachable,
                    message = tcpReachable ? "TCP endpoint reachable." : "TCP endpoint is unreachable.",
                    protocol = request.Protocol
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    success = false,
                    message = $"Connection test failed: {ex.Message}"
                });
            }
        });

        app.MapGet("/api/plc/mappings", async (IConfigurationService configService) =>
        {
            var config = await configService.LoadAsync();
            var mappings = config.Communication?.Mappings ?? new List<PlcAddressMapping>();
            return Results.Ok(mappings);
        });

        app.MapPut("/api/plc/mappings", async (
            List<PlcAddressMapping>? mappings,
            IConfigurationService configService) =>
        {
            var config = await configService.LoadAsync();
            config.Communication ??= new CommunicationConfig();
            config.Communication.Mappings = NormalizeMappings(mappings);
            await configService.SaveAsync(config);

            return Results.Ok(new
            {
                message = "PLC mappings saved.",
                mappings = config.Communication.Mappings
            });
        });

        return app;
    }

    private static List<PlcAddressMapping> NormalizeMappings(List<PlcAddressMapping>? mappings)
    {
        if (mappings == null || mappings.Count == 0)
        {
            return new List<PlcAddressMapping>();
        }

        var normalized = new List<PlcAddressMapping>(mappings.Count);
        foreach (var item in mappings)
        {
            if (item == null)
            {
                continue;
            }

            normalized.Add(new PlcAddressMapping
            {
                Name = (item.Name ?? string.Empty).Trim(),
                Address = (item.Address ?? string.Empty).Trim(),
                DataType = string.IsNullOrWhiteSpace(item.DataType) ? "Bool" : item.DataType.Trim(),
                Description = (item.Description ?? string.Empty).Trim(),
                CanWrite = item.CanWrite
            });
        }

        return normalized;
    }

    private static async Task SafeDisconnectAsync(Acme.PlcComm.Interfaces.IPlcClient client)
    {
        try
        {
            await client.DisconnectAsync();
        }
        catch
        {
            // Ignore disconnect errors in test endpoint.
        }
    }

    private static bool TryBuildPlcCommConnectionString(
        PlcTestConnectionRequest request,
        out string connectionString,
        out string protocol)
    {
        protocol = (request.Protocol ?? string.Empty).Trim();
        connectionString = string.Empty;

        if (string.IsNullOrWhiteSpace(protocol))
        {
            protocol = "S7";
        }

        var ipAddress = (request.IpAddress ?? string.Empty).Trim();
        var port = request.Port;

        switch (protocol.ToUpperInvariant())
        {
            case "S7":
            case "SIEMENSS7":
                {
                    var cpu = string.IsNullOrWhiteSpace(request.CpuType) ? "S7-1200" : request.CpuType!.Trim();
                    var rack = request.Rack ?? 0;
                    var slot = request.Slot ?? 1;
                    protocol = "S7";
                    connectionString = $"S7://{ipAddress}:{port}?cpu={Uri.EscapeDataString(cpu)}&rack={rack}&slot={slot}";
                    return true;
                }
            case "MC":
            case "MITSUBISHIMC":
                protocol = "MC";
                connectionString = $"MC://{ipAddress}:{port}";
                return true;
            case "FINS":
            case "OMRONFINS":
                protocol = "FINS";
                connectionString = $"FINS://{ipAddress}:{port}";
                return true;
            default:
                return false;
        }
    }

    private static async Task<bool> TestTcpReachabilityAsync(string host, int port, CancellationToken ct)
    {
        using var tcpClient = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        await tcpClient.ConnectAsync(host, port, timeoutCts.Token);
        return tcpClient.Connected;
    }
}

public class PlcTestConnectionRequest
{
    public string? Protocol { get; set; }
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 102;
    public string? CpuType { get; set; }
    public int? Rack { get; set; }
    public int? Slot { get; set; }
}
