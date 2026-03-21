#!/usr/bin/env dotnet-script
// Usage:
//   dotnet script tools/generate_operator_catalog.csx [repoRoot] [--overwrite] [--enforce-version-bump]
//
// Legacy wrapper.
// This script now delegates to the canonical generator:
//   scripts/OperatorDocGenerator/OperatorDocGenerator.csproj
//
// That generator rebuilds and syncs:
//   - 算子资料/算子名片/catalog.json
//   - 算子资料/算子名片/CATALOG.md
//   - 算子资料/算子名片/CHANGELOG.md
//   - 算子资料/算子名片/version-history.json
//   - 算子资料/算子目录.json
//   - 算子资料/算子目录.md
//   - 算子资料/算子变更记录.md
//   - 算子资料/算子版本记录.json

using System.Diagnostics;

var repoRoot = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();

var passthroughArgs = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
    ? args.Skip(1).ToArray()
    : args;

var generatorProject = Path.Combine(repoRoot, "scripts", "OperatorDocGenerator", "OperatorDocGenerator.csproj");
if (!File.Exists(generatorProject))
{
    Console.Error.WriteLine($"[ERROR] Canonical generator project not found: {generatorProject}");
    return 1;
}

var forwarded = string.Join(" ", new[] { Quote(repoRoot) }.Concat(passthroughArgs.Select(QuoteIfNeeded)));
var commandArgs = $"run --project {Quote(generatorProject)} -- {forwarded}";

Console.WriteLine("[INFO] Delegating catalog generation to scripts/OperatorDocGenerator/OperatorDocGenerator.csproj");
Console.WriteLine($"[INFO] Repo root: {repoRoot}");

return RunProcess("dotnet", commandArgs, repoRoot);

static string Quote(string value) => $"\"{value}\"";

static string QuoteIfNeeded(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return Quote(value);
    }

    return value.Contains(' ') || value.Contains('"')
        ? Quote(value.Replace("\"", "\\\""))
        : value;
}

static int RunProcess(string fileName, string arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var process = Process.Start(startInfo);
    if (process == null)
    {
        Console.Error.WriteLine($"[ERROR] Failed to start process: {fileName}");
        return -1;
    }

    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data != null)
        {
            Console.WriteLine(eventArgs.Data);
        }
    };

    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data != null)
        {
            Console.Error.WriteLine(eventArgs.Data);
        }
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
    return process.ExitCode;
}
