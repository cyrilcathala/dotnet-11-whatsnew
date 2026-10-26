# Nouveautés .NET 11 démontrées — Mission Control 🚀

Liste de référence pour le slide récapitulatif de fin de démo. Tenue à jour au fil des décisions.

Légende : 🆕 nouveau en .NET 11 · 🔁 rappel .NET 10 (montré mais pas nouveau) · ❓ à confirmer pendant l'implémentation

## 00 — Lancement
- 🆕 `dotnet watch` intégré à l'AppHost Aspire (SDK 11) : vérifié sur la RC3 avec Aspire 13.6.4, une modification de `Home.razor` dans l'API est appliquée à chaud (« C# and Razor changes applied »)
- 🆕 Template `xunit` du SDK 11 avec `--xunit-version v3` : xUnit v3 sur Microsoft.Testing.Platform (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`)
  - ⚠️ Écart constaté sur la RC3 : le template `xunit` génère encore xUnit **v2** par défaut ; il faut passer `--xunit-version v3`
- ℹ️ Aspire 13.6 émet l'avertissement `ASPIRE010` quand l'AppHost est lancé sans la CLI Aspire (`AspireUseCliBundle=false`) ; supprimé dans le `.csproj` de l'AppHost pour garder un build sans warning

## 01 — Enregistrer une sonde
- 🆕 Validation async : `AsyncValidationAttribute`, `IAsyncValidatableObject`, `Validator.ValidateObjectAsync`
- 🆕 Validation async de bout en bout dans les Minimal APIs
- 🆕 `EditForm` Blazor qui attend les validateurs async
- 🆕 Localisation intégrée des messages de validation (`AddLocalization` + `AddValidation`)

## 02 — Envoyer des commandes
- 🆕 C# 15 : union types (`CommandResult`) et switch exhaustif
- 🆕 C# 15 : closed hierarchies (hiérarchie des commandes)
- 🆕 System.Text.Json : sérialisation/désérialisation des unions (cas actif écrit directement, sans `$type`)
- 🆕 `[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]` pour distinguer des cas objets
- 🆕 Mini-bonus : `union SensorValue(double, string, bool)`, cas choisi d'après le type de token JSON
- 🆕 Minimal APIs : unions en body JSON, décrites en `anyOf` dans OpenAPI

## 03 — Petits raffinements C# 15
- 🆕 Collection expression arguments (`[with(...), ..]`)
- 🆕 Extension indexers

## 04 — Pluie de télémétrie (import JSON / JSONL / CBOR)
- 🆕 `ZstandardCompressedContent` (compression du body de requête côté `HttpClient`)
- 🆕 `ZstandardStream` dans `System.IO.Compression`
- ❓ Décompression zstd côté ASP.NET Core (natif ou `IDecompressionProvider` maison)
- 🆕 `CborReader` incrémental (`isFinalBlock: false` + `SlideData`)
- 🆕 Stream adapters sans copie : `ReadOnlySequenceStream`, `ReadOnlyMemoryStream`, `WritableMemoryStream`, `StringStream`
- 🆕 `System.IO.Pipelines` : moins de contention lecteur/écrivain (interne)
- 🔁 `JsonSerializer.DeserializeAsync(PipeReader)` / `Request.BodyReader`
- Banc d'essai : octets réseau, octets du payload, allocations, CPU, débit par format × compression (métriques OTel dans Aspire)

## 05 — Rapport de mission
- 🆕 LINQ `FullJoin` (`Enumerable` / `AsyncEnumerable`)
- 🆕 `JsonSerializer.SerializeAsyncEnumerable` vers un `PipeWriter`, sortie JSON Lines (`topLevelValues: true`)

## 06 — « Houston, on a un problème »
- 🆕 Runtime Async : stack traces propres, moins d'allocations (comparaison net10.0 / net11.0 + BenchmarkDotNet)

## Bonus en rafale
- 🆕 `EqualityComparer<T>.Create` (comparateur depuis un sélecteur de clé)
- 🆕 C# 15 : `break` / `continue` labellisés
- 🆕 `Process` : helpers run-and-capture
- 🆕 File-based apps : `#:include`, `dotnet run -e`
- 🆕 Templates de tests en xUnit v3 sur Microsoft.Testing.Platform, affichage live de `dotnet test`
