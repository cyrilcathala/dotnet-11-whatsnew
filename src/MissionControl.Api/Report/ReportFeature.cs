namespace MissionControl.Api.Report;

/// <summary>
/// Rapport de mission : services et endpoints de la fonctionnalité.
/// </summary>
public static class ReportFeature
{
    public static IServiceCollection AddMissionReport(this IServiceCollection services) =>
        services.AddSingleton<MissionReport>();

    public static IEndpointRouteBuilder MapMissionReport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/mission/report", (MissionReport report) => report.Build())
            .WithTags("Mission");

        return endpoints;
    }
}
