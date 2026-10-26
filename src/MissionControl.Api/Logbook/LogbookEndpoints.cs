using System.Text.Json;
using MissionControl.Telemetry;

namespace MissionControl.Api.Logbook;

public static class LogbookEndpoints
{
    public const string MediaType = "application/jsonl";

    public static IServiceCollection AddLogbook(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LogbookOptions>(configuration.GetSection("Logbook"));
        return services.AddSingleton<CrewLogbook>();
    }

    public static IEndpointRouteBuilder MapLogbook(this IEndpointRouteBuilder app)
    {
        app.MapGet("/logbook/export", ExportAsync)
            .WithTags("Journal de bord")
            .Produces(StatusCodes.Status200OK, contentType: MediaType);

        return app;
    }

    /// <summary>Exporte le journal de bord en JSON Lines, diffusé en flux dans la réponse.</summary>
    private static async Task ExportAsync(HttpContext context, CrewLogbook logbook, CancellationToken cancellationToken)
    {
        context.Response.ContentType = MediaType;
        context.Response.Headers.ContentDisposition = "attachment; filename=journal-de-bord.jsonl";

        // 🆕 .NET 11 : SerializeAsyncEnumerable écrit directement dans le PipeWriter de la réponse.
        // topLevelValues: true → un document JSON compact par entrée, terminé par LF, sans tableau englobant.
        await JsonSerializer.SerializeAsyncEnumerable(
            context.Response.BodyWriter,
            logbook.ReadAsync(cancellationToken),
            TelemetryJsonContext.Default.TelemetryPacket,
            topLevelValues: true,
            cancellationToken);
    }
}
