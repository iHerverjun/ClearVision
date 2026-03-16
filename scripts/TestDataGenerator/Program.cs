using Acme.Product.Infrastructure.TestData;

var repoRoot = ResolveRepoRoot();
var outputDirectory = ResolveOutputDirectory(args, repoRoot);

Directory.CreateDirectory(outputDirectory);

var generated = TestDataGenerator.GenerateAll(outputDirectory);

Console.WriteLine($"Generated {generated.Count} files:");
foreach (var file in generated)
{
    Console.WriteLine(file);
}

return 0;

static string ResolveOutputDirectory(string[] args, string repoRoot)
{
    string? outputArg = null;

    for (int i = 0; i < args.Length; i++)
    {
        if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
        {
            outputArg = args[i + 1];
            i++;
            continue;
        }

        if (!args[i].StartsWith("-", StringComparison.Ordinal) && outputArg is null)
        {
            outputArg = args[i];
        }
    }

    if (!string.IsNullOrWhiteSpace(outputArg))
    {
        return Path.GetFullPath(outputArg);
    }

    return Path.Combine(repoRoot, "Acme.Product", "tests", "TestData");
}

static string ResolveRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, ".git")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }

    return Directory.GetCurrentDirectory();
}
