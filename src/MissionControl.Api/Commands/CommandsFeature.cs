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
        commands.MapPost("/", Results<Ok<CommandResult>, NotFound<ProblemDetails>, Conflict<ProblemDetails>> (ProbeCommand command, CommandCenter center) =>
        {
            var status = center.Send(command, out var result);
            if (status is SendStatus.UnknownProbe)
            {
                return TypedResults.NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Sonde inconnue",
                    Detail = $"Sonde inconnue : « {command.ProbeName} ».",
                });
            }
            if (status is SendStatus.DuplicateId)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Identifiant de commande déjà utilisé",
                    Detail = $"Une commande d'identifiant {command.Id} a déjà été envoyée.",
                });
            }
            return TypedResults.Ok(result);
        });

        return endpoints;
    }
}
