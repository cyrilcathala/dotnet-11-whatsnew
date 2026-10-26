using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionControl.Telemetry;

namespace MissionControl.Api.Telemetry;

public static class TelemetryEndpoints
{
    public static IServiceCollection AddTelemetry(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TelemetryStore>();
        services.AddSingleton<ITelemetryImporter, TelemetryImporter>();
        return services;
    }

    public static IEndpointRouteBuilder MapTelemetry(this IEndpointRouteBuilder app)
    {
        var telemetry = app.MapGroup("/telemetry").WithTags("Télémétrie");

        telemetry.MapPost("/import", ImportAsync).DisableAntiforgery();
        telemetry.MapGet("/", (TelemetryStore store) => store.GetStatus());
        telemetry.MapGet("/probes/{probe}/launch", GetLaunchWindow);

        return app;
    }

    /// <summary>
    /// Importe un flux de paquets. Le format est choisi d'après le <c>Content-Type</c>
    /// et le body est lu au fil de l'eau via <c>Request.BodyReader</c>.
    /// </summary>
    private static async Task<Results<Ok<ImportSummary>, ProblemHttpResult>> ImportAsync(
        HttpContext context, ITelemetryImporter importer, CancellationToken cancellationToken)
    {
        if (TelemetryFormats.FindByMediaType(context.Request.ContentType) is not { } format)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Format de télémétrie non pris en charge",
                detail: $"Content-Type acceptés : {string.Join(", ", TelemetryFormats.All.Select(f => f.MediaType))}.");
        }

        // Le payload dépasse largement la limite par défaut (30 Mo) : il est lu en flux, jamais chargé en entier.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = null;
        }

        return TypedResults.Ok(await importer.ImportAsync(context.Request.BodyReader, format, cancellationToken));
    }

    /// <summary>Les premières minutes de vol d'une sonde, extraites de son enregistrement de début de vol.</summary>
    private static Results<Ok<LaunchWindow>, ProblemHttpResult> GetLaunchWindow(
        string probe, TelemetryStore store, int minutes = 5)
    {
        if (store.GetLaunchRecord(probe) is not { Count: > 0 } record)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: $"Aucune télémétrie reçue de la sonde {probe}");
        }

        var from = record.Min(p => p.Timestamp);
        var to = from.AddMinutes(minutes);
        return TypedResults.Ok(new LaunchWindow(record[0].Probe, from, to, record[from, to]));
    }
}

public sealed record LaunchWindow(string Probe, DateTimeOffset From, DateTimeOffset To, IReadOnlyList<TelemetryPacket> Packets);
