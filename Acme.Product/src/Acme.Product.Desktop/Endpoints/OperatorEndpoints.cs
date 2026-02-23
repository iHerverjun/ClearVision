// OperatorEndpoints.cs
using Acme.Product.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Acme.Product.Desktop.Endpoints;

public static class OperatorEndpoints
{
    public static IEndpointRouteBuilder MapOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/operators/metadata", (IOperatorFactory factory) =>
        {
            var metadata = factory.GetAllMetadata();
            return Results.Ok(metadata);
        });

        return app;
    }
}
