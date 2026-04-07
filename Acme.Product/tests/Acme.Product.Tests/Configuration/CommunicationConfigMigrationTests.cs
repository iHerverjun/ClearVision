using System.Text.Json;
using Acme.Product.Core.Entities;
using FluentAssertions;

namespace Acme.Product.Tests.Configuration;

public class CommunicationConfigMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Normalize_ShouldMigrateLegacyFlatCommunicationIntoMatchingProfile()
    {
        const string json = """
        {
          "communication": {
            "plcIpAddress": "192.168.3.5",
            "plcPort": 5002,
            "protocol": "MC",
            "mappings": [
              { "name": "Trigger", "address": "D100", "dataType": "Word", "description": "trigger", "canWrite": false }
            ]
          }
        }
        """;

        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;
        config.Normalize();

        config.Communication.ActiveProtocol.Should().Be("MC");
        config.Communication.Mc.IpAddress.Should().Be("192.168.3.5");
        config.Communication.Mc.Port.Should().Be(5002);
        config.Communication.Mc.Mappings.Should().ContainSingle();
        config.Communication.Mc.Mappings[0].Address.Should().Be("D100");
        config.Communication.PlcIpAddress.Should().BeNull();
        config.Communication.Mappings.Should().BeNull();
    }

    [Fact]
    public void Normalize_ShouldPreserveLegacyConnectionDataWhenProtocolFallsBackToS7()
    {
        const string json = """
        {
          "communication": {
            "ipAddress": "10.0.0.9",
            "port": 502,
            "protocol": "ModbusTcp",
            "mappings": [
              { "name": "Result", "address": "DB1.DBX0.0", "dataType": "Bool", "description": "", "canWrite": true }
            ]
          }
        }
        """;

        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)!;
        config.Normalize();

        config.Communication.ActiveProtocol.Should().Be("S7");
        config.Communication.S7.IpAddress.Should().Be("10.0.0.9");
        config.Communication.S7.Port.Should().Be(502);
        config.Communication.S7.Mappings.Should().ContainSingle();
        config.Communication.S7.Mappings[0].Name.Should().Be("Result");
    }
}
