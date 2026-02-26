using Acme.OperatorLibrary.Modules;
using Acme.Product.Core.Enums;

namespace Acme.OperatorLibrary.SmokeTests;

public class ModuleNamespaceIndexTests
{
    [Fact]
    public void ModuleNamespace_ShouldExposeExpectedOperatorGroups()
    {
        Assert.Contains(
            OperatorType.MeanFilter,
            Acme.OperatorLibrary.ImageProcessing.Operators.Types);

        Assert.Contains(
            OperatorType.CaliperTool,
            Acme.OperatorLibrary.Measurement.Operators.Types);

        Assert.Contains(
            OperatorType.CameraCalibration,
            Acme.OperatorLibrary.Calibration.Operators.Types);

        Assert.Contains(
            OperatorType.ModbusCommunication,
            Acme.OperatorLibrary.Communication.Operators.Types);

        Assert.Contains(
            OperatorType.TryCatch,
            Acme.OperatorLibrary.FlowControl.Operators.Types);

        Assert.Contains(
            OperatorType.DeepLearning,
            Acme.OperatorLibrary.AI.Operators.Types);
    }

    [Fact]
    public void ModuleCatalog_ShouldResolveKnownTypeToExpectedModule()
    {
        Assert.Equal(OperatorModule.ImageProcessing, OperatorModuleCatalog.GetModule(OperatorType.MeanFilter));
        Assert.Equal(OperatorModule.Measurement, OperatorModuleCatalog.GetModule(OperatorType.CaliperTool));
        Assert.Equal(OperatorModule.Calibration, OperatorModuleCatalog.GetModule(OperatorType.CameraCalibration));
        Assert.Equal(OperatorModule.Communication, OperatorModuleCatalog.GetModule(OperatorType.ModbusCommunication));
        Assert.Equal(OperatorModule.FlowControl, OperatorModuleCatalog.GetModule(OperatorType.TryCatch));
        Assert.Equal(OperatorModule.AI, OperatorModuleCatalog.GetModule(OperatorType.DeepLearning));
    }
}
