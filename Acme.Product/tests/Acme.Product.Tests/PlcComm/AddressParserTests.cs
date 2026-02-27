using Acme.PlcComm.Core;
using Acme.PlcComm.Mitsubishi;
using Acme.PlcComm.Omron;
using Acme.PlcComm.Siemens;
using FluentAssertions;

namespace Acme.Product.Tests.PlcComm;

public class AddressParserTests
{
    [Fact]
    public void S7AddressParser_ShouldParse_DbBitAddress()
    {
        var parser = new S7AddressParser();

        var result = parser.Parse("DB1.DBX10.3");

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Content.Should().NotBeNull();
        result.Content!.AreaType.Should().Be("DB");
        result.Content.DbNumber.Should().Be(1);
        result.Content.StartAddress.Should().Be(10);
        result.Content.BitOffset.Should().Be(3);
        result.Content.DataType.Should().Be(PlcDataType.Bit);
    }

    [Fact]
    public void S7AddressParser_ShouldParse_MWAddress()
    {
        var parser = new S7AddressParser();

        var result = parser.Parse("MW0");

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Content.Should().NotBeNull();
        result.Content!.AreaType.Should().Be("M");
        result.Content.StartAddress.Should().Be(0);
        result.Content.BitOffset.Should().Be(-1);
        result.Content.DataType.Should().Be(PlcDataType.Word);
    }

    [Theory]
    [InlineData("D100", 100, PlcDataType.Word, 0xA8)]
    [InlineData("X10", 8, PlcDataType.Bit, 0x9C)]
    [InlineData("B1F", 31, PlcDataType.Bit, 0xA0)]
    public void McAddressParser_ShouldParse_ExpectedFormats(
        string address,
        int expectedStartAddress,
        PlcDataType expectedDataType,
        byte expectedDeviceCode)
    {
        var parser = new McAddressParser();

        var result = parser.Parse(address);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Content.Should().NotBeNull();
        result.Content!.StartAddress.Should().Be(expectedStartAddress);
        result.Content.DataType.Should().Be(expectedDataType);
        result.Content.DeviceCode.Should().Be(expectedDeviceCode);
    }

    [Fact]
    public void FinsAddressParser_ShouldParse_DmWordAddress()
    {
        var parser = new FinsAddressParser();

        var result = parser.Parse("DM100");

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Content.Should().NotBeNull();
        result.Content!.AreaType.Should().Be("DM");
        result.Content.StartAddress.Should().Be(100);
        result.Content.BitOffset.Should().Be(-1);
        result.Content.DataType.Should().Be(PlcDataType.Word);
        result.Content.DeviceCode.Should().Be(0x82);
    }

    [Fact]
    public void FinsAddressParser_ShouldParse_CioBitAddress()
    {
        var parser = new FinsAddressParser();

        var result = parser.Parse("CIO10.3");

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Content.Should().NotBeNull();
        result.Content!.AreaType.Should().Be("CIO");
        result.Content.StartAddress.Should().Be(10);
        result.Content.BitOffset.Should().Be(3);
        result.Content.DataType.Should().Be(PlcDataType.Bit);
        result.Content.DeviceCode.Should().Be(0x30);
    }

    [Fact]
    public void FinsAddressParser_ShouldParse_EmBankAddress()
    {
        var parser = new FinsAddressParser();

        var result = parser.Parse("EM1 100");

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Content.Should().NotBeNull();
        result.Content!.AreaType.Should().Be("EM");
        result.Content.DbNumber.Should().Be(1);
        result.Content.StartAddress.Should().Be(100);
        result.Content.BitOffset.Should().Be(-1);
        result.Content.DataType.Should().Be(PlcDataType.Word);
        result.Content.DeviceCode.Should().Be(0xA1);
    }
}
