// ImageOutputLifecycleGuardTests.cs
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Acme.Product.Tests.Operators;

public class ImageOutputLifecycleGuardTests
{
    [Fact]
    public void Operators_ShouldNotUseUsingScopedMatAsImageOutputSink()
    {
        var operatorsDir = ResolveOperatorsDirectory();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(operatorsDir, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                if (!TryGetUsingScopedVariable(lines[i], out var variableName))
                {
                    continue;
                }

                var createOutputPattern = $@"CreateImageOutput\(\s*{Regex.Escape(variableName)}\s*(,|\))";
                var wrapPattern = $@"new\s+ImageWrapper\(\s*{Regex.Escape(variableName)}\s*\)";
                var maxLine = Math.Min(i + 180, lines.Length - 1);

                for (var j = i + 1; j <= maxLine; j++)
                {
                    if (!Regex.IsMatch(lines[j], createOutputPattern) && !Regex.IsMatch(lines[j], wrapPattern))
                    {
                        continue;
                    }

                    var relativePath = Path.GetRelativePath(operatorsDir, file).Replace('\\', '/');
                    violations.Add($"{relativePath}:{i + 1} -> {j + 1} ({variableName})");
                    break;
                }
            }
        }

        violations.Should().BeEmpty(
            "using-scoped Mats are disposed before downstream consumers can read the wrapped image");
    }

    private static bool TryGetUsingScopedVariable(string line, out string variableName)
    {
        var usingVarMatch = Regex.Match(line, @"using\s+var\s+([A-Za-z_][A-Za-z0-9_]*)\s*=");
        if (usingVarMatch.Success)
        {
            variableName = usingVarMatch.Groups[1].Value;
            return true;
        }

        var usingBlockMatch = Regex.Match(line, @"using\s*\(\s*(?:var|Mat)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=");
        if (usingBlockMatch.Success)
        {
            variableName = usingBlockMatch.Groups[1].Value;
            return true;
        }

        variableName = string.Empty;
        return false;
    }

    private static string ResolveOperatorsDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Acme.Product.Infrastructure", "Operators");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the operators source directory.");
    }
}
