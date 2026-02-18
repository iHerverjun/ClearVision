// ResultAnalysisServiceTests.cs
// ResultAnalysisService 集成测试
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace Acme.Product.Tests.Integration;

/// <summary>
/// ResultAnalysisService 集成测试
/// </summary>
public class ResultAnalysisServiceIntegrationTests
{
    private readonly IInspectionResultRepository _resultRepository;
    private readonly ResultAnalysisService _analysisService;

    public ResultAnalysisServiceIntegrationTests()
    {
        _resultRepository = Substitute.For<IInspectionResultRepository>();
        _analysisService = new ResultAnalysisService(_resultRepository);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithResults_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var expectedStats = new InspectionStatistics
        {
            TotalCount = 100,
            OKCount = 80,
            NGCount = 15,
            ErrorCount = 5,
            OKRate = 0.8,
            AverageProcessingTimeMs = 150.5
        };

        _resultRepository.GetStatisticsAsync(projectId, null, null)
            .Returns(expectedStats);
        _resultRepository.GetDefectDistributionAsync(projectId, null, null)
            .Returns(new Dictionary<DefectType, int> { { DefectType.Scratch, 10 } });

        // Act
        var result = await _analysisService.GetStatisticsAsync(projectId);

        // Assert
        result.Should().NotBeNull();
        result.ProjectId.Should().Be(projectId);
        result.TotalCount.Should().Be(100);
        result.OKCount.Should().Be(80);
        result.NGCount.Should().Be(15);
        result.ErrorCount.Should().Be(5);
        result.OKRate.Should().Be(0.8);
        result.NGRate.Should().Be(0.15);
        result.ErrorRate.Should().Be(0.05);
        result.AverageProcessingTimeMs.Should().Be(150.5);
        result.TotalDefects.Should().Be(10);
    }

    [Fact]
    public async Task GetDefectDistributionAsync_ShouldReturnSortedDistribution()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var distribution = new Dictionary<DefectType, int>
        {
            { DefectType.Scratch, 10 },
            { DefectType.Stain, 5 },
            { DefectType.ForeignObject, 15 },
            { DefectType.Deformation, 3 }
        };

        _resultRepository.GetDefectDistributionAsync(projectId, null, null)
            .Returns(distribution);

        // Act
        var result = await _analysisService.GetDefectDistributionAsync(projectId);

        // Assert
        result.Should().NotBeNull();
        result.ProjectId.Should().Be(projectId);
        result.TotalDefects.Should().Be(33);
        result.Items.Should().HaveCount(4);
        
        // 验证按数量降序排序
        result.Items[0].DefectType.Should().Be(DefectType.ForeignObject.ToString());
        result.Items[0].Count.Should().Be(15);
        result.Items[0].Percentage.Should().BeApproximately(45.45, 0.1);
    }

    [Fact]
    public async Task GetConfidenceDistributionAsync_ShouldBucketCorrectly()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var results = new List<InspectionResult>
        {
            CreateResultWithDefects(projectId, new[] { 0.95, 0.92 }),
            CreateResultWithDefects(projectId, new[] { 0.75, 0.82, 0.65 }),
            CreateResultWithDefects(projectId, new[] { 0.55, 0.45 }),
        };

        _resultRepository.GetByProjectIdAsync(projectId, 0, 1000)
            .Returns(results);

        // Act
        var result = await _analysisService.GetConfidenceDistributionAsync(projectId);

        // Assert
        result.Should().NotBeNull();
        result.TotalDefects.Should().Be(7);
        result.Buckets.Should().HaveCount(6);
        
        // 90-100%: 2个 (0.95, 0.92)
        result.Buckets.First(b => b.Range == "90-100%").Count.Should().Be(2);
        
        // 80-90%: 1个 (0.82)
        result.Buckets.First(b => b.Range == "80-90%").Count.Should().Be(1);
        
        // 70-80%: 1个 (0.75)
        result.Buckets.First(b => b.Range == "70-80%").Count.Should().Be(1);
        
        // <50%: 1个 (0.45)
        result.Buckets.First(b => b.Range == "<50%").Count.Should().Be(1);
    }

    [Fact]
    public async Task GetTrendAnalysisAsync_WithNoResults_ShouldReturnEmptyDataPoints()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var startTime = new DateTime(2026, 1, 1);
        var endTime = new DateTime(2026, 1, 3);

        _resultRepository.GetByTimeRangeAsync(projectId, startTime, endTime)
            .Returns(new List<InspectionResult>());

        // Act
        var result = await _analysisService.GetTrendAnalysisAsync(
            projectId, TrendInterval.Day, startTime, endTime);

        // Assert
        result.Should().NotBeNull();
        result.ProjectId.Should().Be(projectId);
        result.Interval.Should().Be("Day");
        result.DataPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportToCsvAsync_ShouldGenerateCorrectFormat()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var results = new List<InspectionResult>
        {
            CreateResult(projectId, DateTime.UtcNow, InspectionStatus.OK, 150),
            CreateResultWithDefect(projectId, DateTime.UtcNow.AddMinutes(-5), DefectType.Scratch, 0.85),
        };

        _resultRepository.GetByTimeRangeAsync(projectId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(results);

        // Act
        var csv = await _analysisService.ExportToCsvAsync(projectId);

        // Assert
        csv.Should().NotBeNullOrEmpty();
        csv.Should().Contain("检测ID,工程ID,检测时间,状态,处理时间(ms),置信度,缺陷数量,错误信息");
        csv.Should().Contain(projectId.ToString());
        csv.Should().Contain("OK");
        csv.Should().Contain("NG");
        csv.Should().Contain(DefectType.Scratch.ToString());
    }

    [Fact]
    public async Task ExportToJsonAsync_ShouldGenerateValidJson()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var results = new List<InspectionResult>
        {
            CreateResult(projectId, DateTime.UtcNow, InspectionStatus.OK, 150),
        };

        _resultRepository.GetByTimeRangeAsync(projectId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(results);

        // Act
        var json = await _analysisService.ExportToJsonAsync(projectId);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("ProjectId");
        json.Should().Contain("ExportTime");
        json.Should().Contain(projectId.ToString());
        json.Should().Contain("Results");
    }

    [Fact]
    public async Task GenerateReportAsync_ShouldIncludeAllSections()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var stats = new InspectionStatistics
        {
            TotalCount = 100,
            OKCount = 75,
            NGCount = 20,
            ErrorCount = 5,
            OKRate = 0.75,
            AverageProcessingTimeMs = 200
        };

        _resultRepository.GetStatisticsAsync(projectId, null, null).Returns(stats);
        _resultRepository.GetDefectDistributionAsync(projectId, null, null)
            .Returns(new Dictionary<DefectType, int> { { DefectType.Scratch, 20 } });
        _resultRepository.GetByProjectIdAsync(projectId, 0, 1000).Returns(new List<InspectionResult>());
        _resultRepository.GetByTimeRangeAsync(projectId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<InspectionResult>());

        // Act
        var report = await _analysisService.GenerateReportAsync(projectId);

        // Assert
        report.Should().NotBeNull();
        report.ProjectId.Should().Be(projectId);
        report.GeneratedAt.Should().BeWithin(TimeSpan.FromSeconds(1));
        report.Summary.Should().NotBeNull();
        report.DefectDistribution.Should().NotBeNull();
        report.ConfidenceDistribution.Should().NotBeNull();
        report.HourlyTrend.Should().NotBeNull();
        
        // OK率低于80%，应该有建议
        report.Recommendations.Should().NotBeEmpty();
        report.Recommendations.Should().Contain(r => r.Contains("OK率较低"));
    }

    [Fact]
    public async Task ComparePeriodsAsync_WithStatistics_ShouldReturnComparison()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var period1Start = new DateTime(2026, 1, 1);
        var period1End = new DateTime(2026, 1, 15);
        var period2Start = new DateTime(2026, 1, 16);
        var period2End = new DateTime(2026, 1, 31);

        var period1Stats = new InspectionStatistics
        {
            TotalCount = 100,
            OKCount = 80,
            NGCount = 15,
            ErrorCount = 5,
            OKRate = 0.8,
            AverageProcessingTimeMs = 150
        };
        var period2Stats = new InspectionStatistics
        {
            TotalCount = 120,
            OKCount = 90,
            NGCount = 25,
            ErrorCount = 5,
            OKRate = 0.75,
            AverageProcessingTimeMs = 140
        };

        // Setup mock with specific arguments
        _resultRepository.GetStatisticsAsync(projectId, period1Start, period1End)
            .Returns(Task.FromResult(period1Stats));
        _resultRepository.GetStatisticsAsync(projectId, period2Start, period2End)
            .Returns(Task.FromResult(period2Stats));
        _resultRepository.GetDefectDistributionAsync(projectId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new Dictionary<DefectType, int>());

        // Act
        var comparison = await _analysisService.ComparePeriodsAsync(
            projectId, period1Start, period1End, period2Start, period2End);

        // Assert
        comparison.Should().NotBeNull();
        comparison.ProjectId.Should().Be(projectId);
        comparison.Comparisons.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDefectHeatmapAsync_ShouldGenerateCorrectGrid()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var results = new List<InspectionResult>
        {
            CreateResultWithDefectAt(projectId, DefectType.Scratch, 10, 10, 5, 5),
            CreateResultWithDefectAt(projectId, DefectType.Stain, 20, 20, 5, 5),
            CreateResultWithDefectAt(projectId, DefectType.Scratch, 15, 15, 5, 5),
        };

        _resultRepository.GetByTimeRangeAsync(projectId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(results);

        // Act
        var heatmap = await _analysisService.GetDefectHeatmapAsync(projectId);

        // Assert
        heatmap.Should().NotBeNull();
        heatmap.ProjectId.Should().Be(projectId);
        heatmap.TotalDefects.Should().Be(3);
        heatmap.GridSize.Should().Be(10);
        heatmap.Cells.Should().HaveCount(100); // 10x10 grid
        heatmap.ImageBounds.Should().NotBeNull();
        
        // 至少有一些单元格应该有缺陷
        heatmap.Cells.Any(c => c.DefectCount > 0).Should().BeTrue();
    }

    #region Helper Methods

    private static InspectionResult CreateResult(Guid projectId, DateTime time, InspectionStatus status, long processingTime)
    {
        var result = new InspectionResult(projectId);
        result.SetResult(status, processingTime, 0.9);
        return result;
    }

    private static InspectionResult CreateResultWithDefects(Guid projectId, double[] confidenceScores)
    {
        var result = new InspectionResult(projectId);
        result.SetResult(InspectionStatus.NG, 150, null);
        
        foreach (var score in confidenceScores)
        {
            result.AddDefect(new Defect(
                result.Id,
                DefectType.Scratch,
                10, 10, 5, 5,
                score,
                "Test defect"));
        }
        
        return result;
    }

    private static InspectionResult CreateResultWithDefect(Guid projectId, DateTime time, DefectType type, double confidence)
    {
        var result = new InspectionResult(projectId);
        result.SetResult(InspectionStatus.NG, 150, null);
        result.AddDefect(new Defect(
            result.Id,
            type,
            10, 10, 5, 5,
            confidence,
            "Test defect"));
        return result;
    }

    private static InspectionResult CreateResultWithDefectAt(Guid projectId, DefectType type, double x, double y, double w, double h)
    {
        var result = new InspectionResult(projectId);
        result.SetResult(InspectionStatus.NG, 150, null);
        result.AddDefect(new Defect(
            result.Id,
            type,
            x, y, w, h,
            0.85,
            "Test defect"));
        return result;
    }

    #endregion
}
