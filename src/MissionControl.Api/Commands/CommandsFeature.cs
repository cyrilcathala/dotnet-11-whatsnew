using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MissionControl.Api.Commands;

/// <summary>
/// Centre de commandes : services et endpoints de la fonctionnalité.
/// </summary>
public static class CommandsFeature
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddSingleton<CommandHistory>();
        services.AddSingleton<CommandCenter>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapCommands(this IEndpointRouteBuilder endpoints)
    {
        var commands = endpoints.MapGroup("/commands").WithTags("Commandes");

        commands.MapGet("/", (CommandHistory history) => history.Entries);

        commands.MapGet("/{id:guid}", Results<Ok<ProbeCommand>, NotFound> (Guid id, CommandHistory history) =>
            history.Commands[id] is { } command ? TypedResults.Ok(command) : TypedResults.NotFound());

        // 🆕 .NET 11 : l'union CommandResult est décrite en anyOf de ses cas dans le document OpenAPI.
        commands.MapPost("/", Results<Ok<CommandResult>, NotFound<ProblemDetails>> (ProbeCommand command, CommandCenter center) =>
            center.TrySend(command, out var result)
                ? TypedResults.Ok(result)
                : TypedResults.NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Sonde inconnue",
                    Detail = $"Sonde inconnue : « {command.ProbeName} ».",
                }));

        return endpoints;
    }
}
