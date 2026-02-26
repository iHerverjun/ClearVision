using System.Collections.Generic;
using Acme.Product.Core.Enums;
using Acme.OperatorLibrary.Modules;

namespace Acme.OperatorLibrary.ImageProcessing
{
    public static class Operators
    {
        public static IReadOnlyList<OperatorType> Types => OperatorModuleCatalog.ImageProcessingTypes;
    }
}

namespace Acme.OperatorLibrary.Measurement
{
    public static class Operators
    {
        public static IReadOnlyList<OperatorType> Types => OperatorModuleCatalog.MeasurementTypes;
    }
}

namespace Acme.OperatorLibrary.Calibration
{
    public static class Operators
    {
        public static IReadOnlyList<OperatorType> Types => OperatorModuleCatalog.CalibrationTypes;
    }
}

namespace Acme.OperatorLibrary.Communication
{
    public static class Operators
    {
        public static IReadOnlyList<OperatorType> Types => OperatorModuleCatalog.CommunicationTypes;
    }
}

namespace Acme.OperatorLibrary.FlowControl
{
    public static class Operators
    {
        public static IReadOnlyList<OperatorType> Types => OperatorModuleCatalog.FlowControlTypes;
    }
}

namespace Acme.OperatorLibrary.AI
{
    public static class Operators
    {
        public static IReadOnlyList<OperatorType> Types => OperatorModuleCatalog.AiTypes;
    }
}
