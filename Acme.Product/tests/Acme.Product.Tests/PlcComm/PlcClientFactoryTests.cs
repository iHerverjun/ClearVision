using Acme.PlcComm;
using Acme.PlcComm.Interfaces;
using Acme.PlcComm.Siemens;
using FluentAssertions;

namespace Acme.Product.Tests.PlcComm;

public class PlcClientFactoryTests
{
    [Fact]
    public void CreateFromConnectionString_WithValidS7Uri_ShouldParseCpuRackAndSlot()
    {
        using var client = PlcClientFactory.CreateFromConnectionString(
            "S7://192.168.0.1:102?cpu=S7-1500&rack=1&slot=2");

        client.Should().BeOfType<SiemensS7Client>();
        var s7Client = (SiemensS7Client)client;
        s7Client.IpAddress.Should().Be("192.168.0.1");
        s7Client.Port.Should().Be(102);
        s7Client.CpuType.Should().Be(SiemensCpuType.S71500);
        s7Client.Rack.Should().Be(1);
        s7Client.Slot.Should().Be(2);
    }

    [Fact]
    public void CreateFromConnectionString_WithInvalidUri_ShouldThrowHelpfulArgumentException()
    {
        var act = () => PlcClientFactory.CreateFromConnectionString("not a plc uri");

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*无效的 PLC 连接字符串*");
    }

    [Fact]
    public void CreateFromConnectionString_WithInvalidRack_ShouldThrowHelpfulArgumentException()
    {
        var act = () => PlcClientFactory.CreateFromConnectionString(
            "S7://192.168.0.1:102?cpu=S7-1200&rack=abc&slot=1");

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*rack*无效*");
    }
}
