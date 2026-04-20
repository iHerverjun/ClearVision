using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using FluentAssertions;

namespace Acme.Product.Tests.Services;

public class InspectionJudgmentResolverTests
{
    [Fact]
    public void DetermineStatusFromFlowOutput_WhenAcceptedIsFalse_ShouldReturnNg()
    {
        var evaluation = InspectionJudgmentResolver.DetermineStatusFromFlowOutput(new Dictionary<string, object>
        {
            ["Accepted"] = false
        });

        evaluation.Status.Should().Be(InspectionStatus.NG);
        evaluation.JudgmentSource.Should().Be("Accepted");
        evaluation.StatusReason.Should().Be("DerivedFromAccepted");
        evaluation.MissingJudgmentSignal.Should().BeFalse();
    }

    [Fact]
    public void DetermineStatusFromFlowOutput_WhenVerificationFailsButIsMatchedTrue_ShouldPreferVerificationPassed()
    {
        var evaluation = InspectionJudgmentResolver.DetermineStatusFromFlowOutput(new Dictionary<string, object>
        {
            ["IsMatched"] = true,
            ["VerificationPassed"] = false
        });

        evaluation.Status.Should().Be(InspectionStatus.NG);
        evaluation.JudgmentSource.Should().Be("VerificationPassed");
        evaluation.StatusReason.Should().Be("DerivedFromVerificationPassed");
    }

    [Fact]
    public void DetermineStatusFromFlowOutput_WhenNestedDiagnosticsContainHueValid_ShouldReturnOk()
    {
        var evaluation = InspectionJudgmentResolver.DetermineStatusFromFlowOutput(new Dictionary<string, object>
        {
            ["Diagnostics"] = new Dictionary<string, object>
            {
                ["HueValid"] = true
            }
        });

        evaluation.Status.Should().Be(InspectionStatus.OK);
        evaluation.JudgmentSource.Should().Be("Diagnostics.HueValid");
        evaluation.StatusReason.Should().Be("DerivedFromHueValid");
        evaluation.MissingJudgmentSignal.Should().BeFalse();
    }

    [Fact]
    public void DetermineStatusFromFlowOutput_WhenNoJudgmentSignalExists_ShouldFailClosed()
    {
        var evaluation = InspectionJudgmentResolver.DetermineStatusFromFlowOutput(new Dictionary<string, object>
        {
            ["Message"] = "Only diagnostics text"
        });

        evaluation.Status.Should().Be(InspectionStatus.Error);
        evaluation.JudgmentSource.Should().Be("None");
        evaluation.StatusReason.Should().Be("MissingJudgmentSignal");
        evaluation.MissingJudgmentSignal.Should().BeTrue();
    }
}
