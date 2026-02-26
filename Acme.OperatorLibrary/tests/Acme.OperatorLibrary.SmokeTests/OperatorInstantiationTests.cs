using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.OperatorLibrary.SmokeTests;

public class OperatorInstantiationTests
{
    [Fact]
    public void MeanFilterOperator_ShouldInstantiateFromNuGetPackage()
    {
        var op = new MeanFilterOperator(NullLogger<MeanFilterOperator>.Instance);

        Assert.Equal(OperatorType.MeanFilter, op.OperatorType);
    }
}
