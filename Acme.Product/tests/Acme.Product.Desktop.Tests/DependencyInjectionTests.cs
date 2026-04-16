using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Data;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Acme.Product.Desktop.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public async Task AddVisionServices_ResolvesInspectionWorkerInterface_ToTheHostedWorkerSingleton()
    {
        var services = new ServiceCollection();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        services.AddLogging();
        services.AddSingleton(lifetime);
        services.AddVisionServices();

        await using var provider = services.BuildServiceProvider();
        var concreteWorker = provider.GetRequiredService<InspectionWorker>();
        var interfaceWorker = provider.GetRequiredService<IInspectionWorker>();
        var hostedWorker = provider.GetServices<IHostedService>().OfType<InspectionWorker>().Single();

        interfaceWorker.Should().BeSameAs(concreteWorker);
        hostedWorker.Should().BeSameAs(concreteWorker);
    }

    [Fact]
    public async Task AddVisionServices_UsesLocalAppDataDatabasePathByDefault()
    {
        var services = CreateServiceCollection();
        services.AddVisionServices();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisionDbContext>();

        var expectedPath = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClearVision",
            "vision.db"));

        dbContext.Database.GetConnectionString().Should().Contain(expectedPath);
    }

    [Fact]
    public async Task AddVisionServices_UsesConfiguredDatabaseOverridePath()
    {
        var overridePath = Path.Combine(Path.GetTempPath(), "ClearVisionTests", Guid.NewGuid().ToString("N"), "override.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DependencyInjection.DatabasePathConfigKey] = overridePath
            })
            .Build();

        var services = CreateServiceCollection();
        services.AddVisionServices(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisionDbContext>();

        dbContext.Database.GetConnectionString().Should().Contain(Path.GetFullPath(overridePath));
    }

    [Fact]
    public void ResolveVisionDatabasePath_MigratesMostRecentLegacyDatabase_ToDefaultLocation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ClearVisionTests", Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(root, "bin");
        var currentDirectory = Path.Combine(root, "cwd");
        var localApplicationData = Path.Combine(root, "localappdata");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(currentDirectory);
        Directory.CreateDirectory(localApplicationData);

        var olderLegacyDb = Path.Combine(baseDirectory, "vision.db");
        var newerLegacyDb = Path.Combine(currentDirectory, "vision.db");
        File.WriteAllText(olderLegacyDb, "older");
        File.WriteAllText(newerLegacyDb, "newer");
        File.SetLastWriteTimeUtc(olderLegacyDb, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(newerLegacyDb, DateTime.UtcNow);

        var resolvedPath = DependencyInjection.ResolveVisionDatabasePath(
            configuredPath: null,
            baseDirectory: baseDirectory,
            currentDirectory: currentDirectory,
            localApplicationDataRoot: localApplicationData);

        resolvedPath.Should().Be(Path.GetFullPath(Path.Combine(localApplicationData, "ClearVision", "vision.db")));
        File.Exists(resolvedPath).Should().BeTrue();
        File.ReadAllText(resolvedPath).Should().Be("newer");
    }

    private static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        services.AddLogging();
        services.AddSingleton(lifetime);
        return services;
    }
}
