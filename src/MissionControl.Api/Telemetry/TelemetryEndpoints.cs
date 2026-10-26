using System.Formats.Cbor;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionControl.Telemetry;

namespace MissionControl.Api.Telemetry;

public static class TelemetryEndpoints
{
    public static IServiceCollection AddTelemetry(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TelemetryStore>();
        services.AddSingleton<AsteroidCatalogStore>();
        services.AddSingleton<TestBench>();
        services.AddHttpContextAccessor();
        services.AddSingleton<TelemetryImporter>();
        // Banc d'essai : l'instrumentation enveloppe l'import (décorateur).
        services.AddSingleton<ITelemetryImporter>(sp => new InstrumentedTelemetryImporter(
            sp.GetRequiredService<TelemetryImporter>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<TestBench>()));
        return services;
    }

    public static IEndpointRouteBuilder MapTelemetry(this IEndpointRouteBuilder app)
    {
        var telemetry = app.MapGroup("/telemetry").WithTags("Télémétrie");

        // La limite de taille s'applique aussi au body décompressé : elle est levée pour tout l'endpoint.
        telemetry.MapPost("/import", ImportAsync).DisableAntiforgery()
            .WithMetadata(new DisableRequestSizeLimitAttribute());
        telemetry.MapPost("/packets", AppendPacket).DisableAntiforgery();
        telemetry.MapGet("/", (TelemetryStore store) => store.GetStatus());
        telemetry.MapGet("/probes/{probe}/launch", GetLaunchWindow);
        telemetry.MapGet("/catalog", GetCatalog);
        telemetry.MapGet("/bench", (TestBench bench) => bench.GetRows());

        return app;
    }

    /// <summary>
    /// Importe un flux de paquets. Le format est choisi d'après le <c>Content-Type</c>
    /// et le body est lu au fil de l'eau via <c>Request.BodyReader</c>.
    /// </summary>
    private static async Task<Results<Ok<ImportSummary>, ProblemHttpResult>> ImportAsync(
        HttpContext context, ITelemetryImporter importer, CancellationToken cancellationToken)
    {
        // Le middleware RequestDecompression retire l'en-tête quand il a décompressé le body :
        // s'il est encore là, aucun décompresseur ne correspondait.
        if (context.Request.Headers.ContentEncoding is { Count: > 0 } encoding)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Compression non prise en charge",
                detail: $"Content-Encoding reçu : {encoding}. Encodage accepté : zstd (br, gzip et deflate aussi), un seul à la fois, ou body non compressé.");
        }

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

        var body = context.Request.BodyReader;
        try
        {
            if (await UndeclaredZstd.DetectAsync(body, cancellationToken) is { } preview)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "Body compressé sans Content-Encoding",
                    detail: $"Le body est une trame zstd : ajoutez l'en-tête Content-Encoding: zstd. Début du contenu décompressé : {preview}");
            }

            return TypedResults.Ok(await importer.ImportAsync(body, format, cancellationToken));
        }
        catch (InvalidDataException exception)
        {
            // Levée par le flux de décompression du middleware quand le body zstd est corrompu.
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Body compressé illisible",
                detail: $"La décompression a échoué ({exception.Message}). Les paquets lus avant l'erreur sont conservés.");
        }
        catch (Exception exception) when (exception is CborContentException or JsonException or OverflowException or FormatException)
        {
            // Levée par le lecteur de format : paquet tronqué, syntaxe invalide, nombre hors limites.
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Payload de télémétrie illisible",
                detail: $"Le contenu {format.Name} est mal formé ({exception.Message}). Les paquets lus avant l'erreur sont conservés.");
        }
    }

    /// <summary>Un relevé isolé, posté en JSON (body lié par Minimal APIs).</summary>
    private static NoContent AppendPacket(TelemetryPacket packet, TelemetryStore store)
    {
        store.Append([packet]);
        return TypedResults.NoContent();
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

    /// <summary>Le dernier catalogue d'astéroïdes importé en JSON.</summary>
    private static Results<Ok<ImportedCatalog>, ProblemHttpResult> GetCatalog(AsteroidCatalogStore catalogs)
        => catalogs.Current is { } catalog
            ? TypedResults.Ok(catalog)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Aucun catalogue d'astéroïdes importé");
}

public sealed record LaunchWindow(string Probe, DateTimeOffset From, DateTimeOffset To, IReadOnlyList<TelemetryPacket> Packets);
