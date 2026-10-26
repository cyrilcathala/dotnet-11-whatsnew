using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Localization;

namespace MissionControl.Api.Probes;

/// <summary>
/// Demande d'enregistrement d'une sonde : même modèle pour <c>POST /probes</c> et pour le formulaire Blazor.
/// </summary>
// 🆕 .NET 11 : IAsyncValidatableObject, une règle multi-champs qui peut attendre un service.
public sealed class ProbeRegistration : IAsyncValidatableObject
{
    [Required(ErrorMessage = "NameRequired")]
    [ProbeNameAvailable(ErrorMessage = "NameTaken")]
    public string Name { get; set; } = "";

    public DateTime LaunchWindowOpensAt { get; set; }

    public DateTime LaunchWindowClosesAt { get; set; }

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext validationContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Les messages renvoyés ici ne passent pas par la localisation intégrée : on traduit nous-mêmes.
        var messages = validationContext.GetRequiredService<IStringLocalizer<ValidationMessages>>();

        if (LaunchWindowClosesAt <= LaunchWindowOpensAt)
        {
            yield return new ValidationResult(messages["LaunchWindowInverted"], [nameof(LaunchWindowClosesAt)]);
            yield break;
        }

        var station = validationContext.GetRequiredService<GroundStation>();
        if (!await station.IsWindowAvailableAsync(LaunchWindowOpensAt, LaunchWindowClosesAt, cancellationToken))
        {
            yield return new ValidationResult(WindowUnavailableMessage(messages), [nameof(LaunchWindowOpensAt)]);
        }
    }

    /// <summary>Fenêtre déjà réservée : message localisé, renvoyé aussi quand l'enregistrement la trouve prise.</summary>
    public string WindowUnavailableMessage(IStringLocalizer<ValidationMessages> messages) => messages["LaunchWindowUnavailable"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        throw new NotSupportedException("Validation asynchrone uniquement : utiliser ValidateAsync.");

    public Probe ToProbe() => new(Name, LaunchWindowOpensAt, LaunchWindowClosesAt);
}
