using System.Globalization;

namespace MissionControl.Telemetry;

/// <summary>
/// Valeur lue sur un capteur : un nombre (température), un texte (mode de vol) ou un booléen (antenne déployée).
/// </summary>
// 🆕 C# 15 : union de trois types existants, sans base commune ni convertisseur STJ.
// En JSON, STJ choisit le cas d'après le type de token (nombre, chaîne, booléen).
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
