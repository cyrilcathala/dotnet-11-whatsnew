using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MissionControl.Api.Diagnostics;

/// <summary>Ce qui tourne sur la station sol : le runtime de l'API et le SDK .NET installé.</summary>
public sealed record MissionDiagnostic(string Runtime, string OperatingSystem, string? Sdk, string? SdkError);

/// <summary>
/// Diagnostic de la station sol : endpoint <c>GET /mission/diagnostic</c>.
/// </summary>
public static class DiagnosticFeature
{
    public static IEndpointRouteBuilder MapMissionDiagnostic(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/mission/diagnostic", DiagnoseAsync)
            .WithTags("Mission");

        return endpoints;
    }

    private static async Task<MissionDiagnostic> DiagnoseAsync(CancellationToken cancellationToken)
    {
        var (sdk, error) = await QuerySdkAsync(cancellationToken);
        return new(RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription, sdk, error);
    }

    private static async Task<(string? Sdk, string? Error)> QuerySdkAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 🆕 .NET 11 : Process.RunAndCaptureTextAsync lance, attend la fin et capture stdout/stderr en un appel.
            var dotnet = await Process.RunAndCaptureTextAsync("dotnet", ["--version"], cancellationToken);

            return dotnet.ExitStatus.ExitCode == 0
                ? (dotnet.StandardOutput.Trim(), null)
                : (null, $"dotnet --version a échoué (code {dotnet.ExitStatus.ExitCode}) : {dotnet.StandardError.Trim()}");
        }
        catch (Win32Exception exception)
        {
            return (null, $"dotnet introuvable : {exception.Message}");
        }
    }
}
