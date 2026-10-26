using System.ComponentModel.DataAnnotations;

namespace MissionControl.Api.Probes;

/// <summary>
/// Le nom de la sonde doit être disponible dans le registre : ni déjà pris (casse ignorée), ni réservé.
/// </summary>
// 🆕 .NET 11 : AsyncValidationAttribute, un attribut de validation qui peut attendre un service.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ProbeNameAvailableAttribute : AsyncValidationAttribute
{
    protected override async Task<ValidationResult?> IsValidAsync(
        object? value, ValidationContext validationContext, CancellationToken cancellationToken)
    {
        if (value is not string { Length: > 0 } name) return ValidationResult.Success;

        var registry = validationContext.GetRequiredService<ProbeRegistry>();
        return await registry.IsNameAvailableAsync(name, cancellationToken)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessageString, [validationContext.MemberName!]);
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        throw new NotSupportedException("Validation asynchrone uniquement : utiliser IsValidAsync.");
}
