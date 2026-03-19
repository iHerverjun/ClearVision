using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
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
}
