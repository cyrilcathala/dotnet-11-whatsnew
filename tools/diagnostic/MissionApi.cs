using System.Text.Json;

/// <summary>
/// Client minimal de l'API Mission Control, partagé par le script via <c>#:include</c>.
/// Les réponses sont lues en <see cref="JsonElement"/> : une file-based app désactive la sérialisation
/// par réflexion (réglages compatibles AOT par défaut).
/// </summary>
sealed class MissionApi(Uri baseAddress) : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(10) };

    public async Task<JsonElement> GetAsync(string path)
    {
        using var response = await _http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Compte les éléments d'un tableau JSON par valeur d'une propriété texte.</summary>
    public static IEnumerable<(string Value, int Count)> CountBy(JsonElement array, string property) =>
        array.EnumerateArray()
            .CountBy(item => item.GetProperty(property).GetString() ?? "?")
            .Select(group => (group.Key, group.Value));

    public void Dispose() => _http.Dispose();
}
