using Acme.Product.Application.Services;
using Acme.Product.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Acme.Product.Desktop.Endpoints;

public static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/stats", async (ISystemStatsService statsService, CancellationToken cancellationToken) =>
        {
            var stats = await statsService.GetDashboardStatsAsync(cancellationToken);
            return Results.Ok(stats);
        });

        app.MapGet("/api/system/hardware", async (ISystemStatsService statsService, CancellationToken cancellationToken) =>
        {
            var status = await statsService.GetHardwareStatusAsync(cancellationToken);
            return Results.Ok(status);
        });

        app.MapGet("/api/activities", async (ISystemStatsService statsService, int count = 10, CancellationToken cancellationToken = default) =>
        {
            var activities = await statsService.GetRecentActivitiesAsync(count, cancellationToken);
            return Results.Ok(activities);
        });

        app.MapGet("/api/system/version", async (ISystemStatsService statsService, CancellationToken cancellationToken) =>
        {
            var version = await statsService.GetAppVersionAsync(cancellationToken);
            return Results.Ok(new { Version = version });
        });

        app.MapGet("/api/projects/{id:guid}/stats", async (Guid id, IInspectionService inspectionService, CancellationToken cancellationToken) =>
        {
            var stats = await inspectionService.GetStatisticsAsync(id, null, null);
            return Results.Ok(stats);
        });

        app.MapGet("/api/inspection/counters", async (ISystemStatsService statsService, CancellationToken cancellationToken) =>
        {
            var stats = await statsService.GetDashboardStatsAsync(cancellationToken);
            return Results.Ok(new
            {
                okCount = stats.OkCount,
                ngCount = stats.NgCount,
                totalCount = stats.TotalInspections,
                yieldRate = stats.TotalInspections > 0
                    ? Math.Round((double)stats.OkCount / stats.TotalInspections * 100, 2)
                    : 0
            });
        });

        return app;
    }
}
