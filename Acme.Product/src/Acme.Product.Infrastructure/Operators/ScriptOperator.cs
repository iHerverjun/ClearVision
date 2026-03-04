// ScriptOperator.cs
// 脚本算子
// 执行内嵌脚本并输出脚本运行结果
// 作者：蘅芜君
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "脚本算子",
    Description = "Runs user-defined expression or script snippet.",
    Category = "逻辑工具",
    IconName = "script",
    Keywords = new[] { "script", "custom", "code", "expression", "formula" }
)]
[InputPort("Input1", "Input 1", PortDataType.Any, IsRequired = false)]
[InputPort("Input2", "Input 2", PortDataType.Any, IsRequired = false)]
[InputPort("Input3", "Input 3", PortDataType.Any, IsRequired = false)]
[InputPort("Input4", "Input 4", PortDataType.Any, IsRequired = false)]
[OutputPort("Output1", "Output 1", PortDataType.Any)]
[OutputPort("Output2", "Output 2", PortDataType.Any)]
[OperatorParam("ScriptLanguage", "Script Language", "enum", DefaultValue = "CSharpExpression", Options = new[] { "CSharpExpression|CSharpExpression", "CSharpScript|CSharpScript" })]
[OperatorParam("Code", "Code", "string", DefaultValue = "Input1 + Input2")]
[OperatorParam("Timeout", "Timeout (ms)", "int", DefaultValue = 5000, Min = 1, Max = 120000)]
public class ScriptOperator : OperatorBase
{
    private static readonly string[] SupportedLanguages = ["CSharpExpression", "CSharpScript"];

    public override OperatorType OperatorType => OperatorType.ScriptOperator;

    public ScriptOperator(ILogger<ScriptOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var language = GetStringParam(@operator, "ScriptLanguage", "CSharpExpression");
        var code = GetStringParam(@operator, "Code", string.Empty).Trim();
        var timeoutMs = GetIntParam(@operator, "Timeout", 5000, 1, 120000);

        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Code is required"));
        }

        if (!SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ScriptLanguage must be CSharpExpression or CSharpScript"));
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        var context = BuildContext(inputs);
        object? output1 = null;
        object? output2 = null;

        var statements = SplitStatements(code);
        if (statements.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Code does not contain executable statements"));
        }

        foreach (var raw in statements)
        {
            cts.Token.ThrowIfCancellationRequested();

            var statement = NormalizeStatement(raw);
            if (string.IsNullOrWhiteSpace(statement))
            {
                continue;
            }

            if (TryParseAssignment(statement, out var target, out var expression))
            {
                var value = EvaluateExpression(expression, context);
                if (target.Equals("Output1", StringComparison.OrdinalIgnoreCase))
                {
                    output1 = value;
                }
                else if (target.Equals("Output2", StringComparison.OrdinalIgnoreCase))
                {
                    output2 = value;
                }
                else
                {
                    context[target] = value;
                }
            }
            else
            {
                output1 = EvaluateExpression(statement, context);
            }
        }

        var result = new Dictionary<string, object>
        {
            { "Output1", output1 ?? string.Empty },
            { "Output2", output2 ?? string.Empty }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(result));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var language = GetStringParam(@operator, "ScriptLanguage", "CSharpExpression");
        if (!SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ScriptLanguage must be CSharpExpression or CSharpScript");
        }

        var code = GetStringParam(@operator, "Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code))
        {
            return ValidationResult.Invalid("Code cannot be empty");
        }

        var timeout = GetIntParam(@operator, "Timeout", 5000);
        if (timeout <= 0)
        {
            return ValidationResult.Invalid("Timeout must be greater than 0");
        }

        return ValidationResult.Valid();
    }

    private static Dictionary<string, object> BuildContext(Dictionary<string, object>? inputs)
    {
        var context = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (inputs != null)
        {
            foreach (var (key, value) in inputs)
            {
                context[key] = value;
            }
        }

        for (var i = 1; i <= 4; i++)
        {
            var key = $"Input{i}";
            if (!context.ContainsKey(key))
            {
                context[key] = 0d;
            }
        }

        return context;
    }

    private static List<string> SplitStatements(string code)
    {
        return code
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string NormalizeStatement(string statement)
    {
        var trimmed = statement.Trim();
        if (trimmed.StartsWith("return ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[7..].Trim();
        }

        return trimmed;
    }

    private static bool TryParseAssignment(string statement, out string target, out string expression)
    {
        target = string.Empty;
        expression = string.Empty;

        var eqIndex = statement.IndexOf('=');
        if (eqIndex <= 0)
        {
            return false;
        }

        target = statement[..eqIndex].Trim();
        expression = statement[(eqIndex + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(expression);
    }

    private static object EvaluateExpression(string expression, IReadOnlyDictionary<string, object> context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var trimmed = expression.Trim();

        if (IsQuotedLiteral(trimmed))
        {
            return trimmed[1..^1];
        }

        if (context.TryGetValue(trimmed, out var directValue))
        {
            return directValue;
        }

        if (bool.TryParse(trimmed, out var booleanResult))
        {
            return booleanResult;
        }

        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericResult))
        {
            return numericResult;
        }

        var numericExpression = ReplaceVariables(trimmed, context);

        try
        {
            using var table = new DataTable();
            var raw = table.Compute(numericExpression, null);

            if (raw is null)
            {
                return string.Empty;
            }

            if (raw is bool b)
            {
                return b;
            }

            return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
        }
        catch
        {
            return trimmed;
        }
    }

    private static bool IsQuotedLiteral(string text)
    {
        return text.Length >= 2 &&
               ((text.StartsWith('"') && text.EndsWith('"')) ||
                (text.StartsWith('\'') && text.EndsWith('\'')));
    }

    private static string ReplaceVariables(string expression, IReadOnlyDictionary<string, object> context)
    {
        var result = expression;

        foreach (var (key, value) in context)
        {
            if (!TryConvertToDouble(value, out var numeric))
            {
                continue;
            }

            var pattern = $@"\b{Regex.Escape(key)}\b";
            result = Regex.Replace(
                result,
                pattern,
                numeric.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }

    private static bool TryConvertToDouble(object? raw, out double value)
    {
        value = 0;
        if (raw is null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            bool b => (value = b ? 1d : 0d) >= 0,
            _ => double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
        };
    }
}

