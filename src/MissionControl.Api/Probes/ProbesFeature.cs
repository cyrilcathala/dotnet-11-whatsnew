using Microsoft.Extensions.Localization;

namespace MissionControl.Api.Probes;

/// <summary>
/// Enregistrement des sondes : services et endpoints de la fonctionnalité.
/// </summary>
public static class ProbesFeature
{
    public static IServiceCollection AddProbes(this IServiceCollection services)
    {
        services.AddSingleton<ProbeRegistry>();
        services.AddSingleton<GroundStation>();

        services.AddProblemDetails();

        // 🆕 .NET 11 : localisation intégrée des messages de validation (Minimal APIs et Blazor).
        services.AddLocalization();
        services.AddValidation(options =>
            options.LocalizerProvider = (_, factory) => factory.Create(typeof(ValidationMessages)));

        return services;
    }

    public static IEndpointRouteBuilder MapProbes(this IEndpointRouteBuilder endpoints)
    {
        var probes = endpoints.MapGroup("/probes").WithTags("Sondes");

        probes.MapGet("/", (ProbeRegistry registry) => registry.Fleet);

        // 🆕 .NET 11 : la validation async (attribut + IAsyncValidatableObject) s'exécute avant le handler.
        // Deux demandes simultanées peuvent passer la validation : l'enregistrement revérifie nom et fenêtre.
        probes.MapPost("/", (ProbeRegistration registration, ProbeRegistry registry, IStringLocalizer<ValidationMessages> messages) =>
        {
            var probe = registration.ToProbe();
            return registry.Register(probe) switch
            {
                RegistrationStatus.NameTaken => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(ProbeRegistration.Name)] = [messages["NameTaken"]],
                }),
                RegistrationStatus.WindowTaken => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(ProbeRegistration.LaunchWindowOpensAt)] = [registration.WindowUnavailableMessage(messages)],
                }),
                _ => Results.Created($"/probes/{Uri.EscapeDataString(probe.Name)}", probe),
            };
        });

        return endpoints;
    }
}
