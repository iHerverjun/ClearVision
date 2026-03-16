using Acme.Product.Infrastructure.Diagnostics;
using FluentAssertions;

namespace Acme.Product.Desktop.Tests.Diagnostics;

public class PerformanceProfilerTests
{
    [Fact]
    public void Profiler_Should_Record_Execution_Time()
    {
        // Arrange
        PerformanceProfiler.Reset();
        var testOperationName = "TestOp1";
        
        // Act
        using (var profiler = new PerformanceProfiler(testOperationName))
        {
            Thread.Sleep(50); // Simulate work
        }
        
        // Assert
        var results = PerformanceProfiler.GetResults().ToList();
        results.Should().HaveCount(1);
        
        var result = results.First();
        result.Name.Should().Be(testOperationName);
        result.Count.Should().Be(1);
        result.TotalMs.Should().BeGreaterThan(40); // Allow some scheduling variance
        result.AverageMs.Should().Be(result.TotalMs);
    }

    [Fact]
    public void Profiler_Should_Aggregate_Multiple_Executions()
    {
        // Arrange
        PerformanceProfiler.Reset();
        var testOperationName = "TestOp2";
        
        // Act
        for (int i = 0; i < 3; i++)
        {
            using (var profiler = new PerformanceProfiler(testOperationName))
            {
                Thread.Sleep(20);
            }
        }
        
        // Assert
        var results = PerformanceProfiler.GetResults().ToList();
        results.Should().HaveCount(1);
        
        var result = results.First();
        result.Count.Should().Be(3);
        result.TotalMs.Should().BeGreaterThan(50);
        result.AverageMs.Should().Be(result.TotalMs / 3);
        result.StdDevMs.Should().BeGreaterThanOrEqualTo(0);
        result.MinMs.Should().BeLessThanOrEqualTo(result.AverageMs);
        result.MaxMs.Should().BeGreaterThanOrEqualTo(result.AverageMs);
    }

    [Fact]
    public void Profiler_Should_Export_Csv()
    {
        // Arrange
        PerformanceProfiler.Reset();
        using (var profiler = new PerformanceProfiler("ExportTest"))
        {
            Thread.Sleep(10);
        }
        
        // Act
        var csv = PerformanceProfiler.GenerateReportCsv();
        
        // Assert
        csv.Should().NotBeNullOrEmpty();
        csv.Should().Contain("OperatorName,Count,TotalMs,AverageMs,MinMs,MaxMs,StdDevMs");
        csv.Should().Contain("ExportTest");
        csv.Should().Contain(",1,"); // Count should be 1
    }
}
