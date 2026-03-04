// ImageOutputLifecycleGuardTests.cs
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Acme.Product.Tests.Operators;

public class ImageOutputLifecycleGuardTests
{
    [Fact]
    public void Operators_ShouldNotPassUsingVarMatDirectlyIntoCreateImageOutput()
    {
        var operatorsDir = ResolveOperatorsDirectory();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(operatorsDir, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var match = Regex.Match(lines[i], @"using\s+var\s+([A-Za-z_][A-Za-z0-9_]*)\s*=");
                if (!match.Success)
                {
                    continue;
                }

                var variableName = match.Groups[1].Value;
                var createOutputPattern = $@"CreateImageOutput\(\s*{Regex.Escape(variableName)}\s*(,|\))";
                var maxLine = Math.Min(i + 140, lines.Length - 1);

                for (var j = i + 1; j <= maxLine; j++)
                {
                    if (!Regex.IsMatch(lines[j], createOutputPattern))
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
            "`using var Mat` + `CreateImageOutput(mat)` disposes the Mat before downstream operators access it");
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
