// Sprint4_DryRunTests.cs
// Sprint 4 Task 4.2 DryRunStubRegistry 单元测试
// 作者：蘅芜君

using Acme.Product.Infrastructure.AI.DryRun;
using Xunit;

namespace Acme.Product.Tests.AI;

/// <summary>
/// Sprint 4 Task 4.2: DryRunStubRegistry 单元测试
/// </summary>
public class Sprint4_DryRunTests
{
    [Fact]
    public void DryRunStubRegistry_RegisterSingleResponse_ReturnsCorrectResponse()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("192.168.1.10:502", "40001",
            StubResponse.ModbusResponse("0001"));

        var response = registry.GetNextResponse("192.168.1.10:502", "40001");

        Assert.True(response.IsSuccess);
        Assert.Equal("0001", response.Payload);
    }

    [Fact]
    public void DryRunStubRegistry_RegisterMultipleResponses_CyclesThrough()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("192.168.1.10:502", "40001",
            StubResponse.ModbusResponse("0001"),
            StubResponse.ModbusResponse("0000"),
            StubResponse.Error("Connection refused"));

        // 第 1 次
        var r1 = registry.GetNextResponse("192.168.1.10:502", "40001");
        Assert.Equal("0001", r1.Payload);

        // 第 2 次
        var r2 = registry.GetNextResponse("192.168.1.10:502", "40001");
        Assert.Equal("0000", r2.Payload);

        // 第 3 次
        var r3 = registry.GetNextResponse("192.168.1.10:502", "40001");
        Assert.False(r3.IsSuccess);

        // 第 4 次 - 循环回到第 1 个
        var r4 = registry.GetNextResponse("192.168.1.10:502", "40001");
        Assert.Equal("0001", r4.Payload);
    }

    [Fact]
    public void DryRunStubRegistry_NoStubRegistered_ReturnsDefaultSuccess()
    {
        var registry = new DryRunStubRegistry();

        var response = registry.GetNextResponse("192.168.1.99:502", "40001");

        Assert.True(response.IsSuccess);
        Assert.Equal("{\"status\":\"OK\"}", response.Payload);
    }

    [Fact]
    public void DryRunStubRegistry_HttpJsonResponse_ReturnsCorrectData()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("https://mes.factory.com", "/api/quality/check",
            StubResponse.JsonResponse(new { result = "PASS", lotId = "LOT_001" }));

        var response = registry.GetNextResponse("https://mes.factory.com", "/api/quality/check");

        Assert.True(response.IsSuccess);
        Assert.Contains("PASS", response.Payload);
        Assert.Contains("LOT_001", response.Payload);
    }

    [Fact]
    public void DryRunStubRegistry_TimeoutResponse_SimulatesTimeout()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("192.168.1.10:502", "40001", StubResponse.Timeout);

        var response = registry.GetNextResponse("192.168.1.10:502", "40001");

        Assert.False(response.IsSuccess);
        Assert.Equal(30000, response.DelayMs);
        Assert.Contains("timed out", response.ErrorMessage);
    }

    [Fact]
    public void DryRunStubRegistry_ErrorResponse_ReturnsError()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("192.168.1.10:502", "40001",
            StubResponse.Error("Device not responding"));

        var response = registry.GetNextResponse("192.168.1.10:502", "40001");

        Assert.False(response.IsSuccess);
        Assert.Equal("Device not responding", response.ErrorMessage);
    }

    [Fact]
    public void DryRunStubRegistry_HasStub_ReturnsCorrectValue()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("192.168.1.10:502", "40001", StubResponse.DefaultSuccess);

        Assert.True(registry.HasStub("192.168.1.10:502", "40001"));
        Assert.False(registry.HasStub("192.168.1.10:502", "40002"));
        Assert.False(registry.HasStub("192.168.1.11:502", "40001"));
    }

    [Fact]
    public void DryRunStubRegistry_GetRegisteredTargets_ReturnsAllTargets()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("192.168.1.10:502", "40001", StubResponse.DefaultSuccess);
        registry.Register("192.168.1.10:502", "40002", StubResponse.DefaultSuccess);
        registry.Register("https://mes.factory.com", "/api/check", StubResponse.DefaultSuccess);

        var targets = registry.GetRegisteredTargets().ToList();

        Assert.Equal(3, targets.Count);
        Assert.Contains("192.168.1.10:502/40001", targets);
        Assert.Contains("192.168.1.10:502/40002", targets);
        Assert.Contains("https://mes.factory.com//api/check", targets);
    }

    [Fact]
    public void DryRunStubRegistry_Clear_RemovesAllStubs()
    {
        var registry = new DryRunStubRegistry();
        registry.Register("192.168.1.10:502", "40001", StubResponse.DefaultSuccess);
        
        registry.Clear();

        Assert.False(registry.HasStub("192.168.1.10:502", "40001"));
        Assert.Empty(registry.GetRegisteredTargets());
    }

    [Fact]
    public void DryRunStubRegistry_ComplexScenario_MultipleDevices()
    {
        // 验证"机械臂状态异常时的报警流程"场景
        var registry = new DryRunStubRegistry()
            .Register("192.168.1.10:502", "40001",  // 机械臂状态寄存器
                new StubResponse(true, "0001"),      // Ready
                new StubResponse(true, "0000"),      // Not Ready
                StubResponse.Error("Connection refused"))
            .Register("https://mes.factory.com", "/api/quality/check",
                StubResponse.JsonResponse(new { result = "PASS" }),
                StubResponse.JsonResponse(new { result = "FAIL", reason = "尺寸超差" }));

        // 验证机械臂状态序列
        Assert.Equal("0001", registry.GetNextResponse("192.168.1.10:502", "40001").Payload);
        Assert.Equal("0000", registry.GetNextResponse("192.168.1.10:502", "40001").Payload);
        Assert.False(registry.GetNextResponse("192.168.1.10:502", "40001").IsSuccess);

        // 验证 MES 响应
        Assert.Contains("PASS", registry.GetNextResponse("https://mes.factory.com", "/api/quality/check").Payload);
        Assert.Contains("FAIL", registry.GetNextResponse("https://mes.factory.com", "/api/quality/check").Payload);
    }
}
