namespace Acme.Product.Core.Enums;

public static class OperatorTypeAliasResolver
{
    private static readonly IReadOnlyDictionary<OperatorType, OperatorType> LegacyAliasMap =
        new Dictionary<OperatorType, OperatorType>
        {
            [OperatorType.Preprocessing] = OperatorType.Filtering,
            [OperatorType.GaussianBlur] = OperatorType.Filtering,
            [OperatorType.OnnxInference] = OperatorType.DeepLearning,
            [OperatorType.ModbusRtuCommunication] = OperatorType.ModbusCommunication
        };

    public static bool IsLegacyAlias(OperatorType type) => LegacyAliasMap.ContainsKey(type);

    public static OperatorType Resolve(OperatorType type)
    {
        return LegacyAliasMap.TryGetValue(type, out var canonicalType)
            ? canonicalType
            : type;
    }
}
