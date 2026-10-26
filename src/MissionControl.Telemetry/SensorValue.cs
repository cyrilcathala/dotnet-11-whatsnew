using System.Globalization;
using System.Text.Json.Serialization;

namespace MissionControl.Telemetry;

/// <summary>
/// Valeur lue sur un capteur : un nombre (température), un texte (mode de vol) ou un booléen (antenne déployée).
/// </summary>
// 🆕 C# 15 : union de trois types existants, sans base commune ni convertisseur STJ.
// En JSON, STJ choisit le cas d'après le type de token (nombre, chaîne, booléen).
// ⚠️ Les options « web » (Minimal APIs) lisent un nombre depuis une chaîne : "ok" serait ambigu
// entre double et string. Strict, posé sur le type, lève l'ambiguïté partout, sans toucher aux options.
[JsonNumberHandling(JsonNumberHandling.Strict)]
public union SensorValue(double, string, bool)
{
    public override string ToString() => this switch
    {
        double reading => reading.ToString("0.##", CultureInfo.CurrentCulture),
        string text => text,
        bool flag => flag ? "oui" : "non",
        null => "—",
    };
}
