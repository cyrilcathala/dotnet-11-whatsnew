# Nouveautés .NET 11 démontrées — Mission Control 🚀

Liste de référence pour le slide récapitulatif de fin de démo. Tenue à jour au fil des décisions.

Légende : 🆕 nouveau en .NET 11 · 🔁 rappel .NET 10 (montré mais pas nouveau) · ❓ à confirmer pendant l'implémentation

## 00 — Lancement
- 🆕 `dotnet watch` intégré à l'AppHost Aspire (SDK 11) : vérifié sur la RC3 avec Aspire 13.6.4, une modification de `Home.razor` dans l'API est appliquée à chaud (« C# and Razor changes applied »)
- 🆕 Template `xunit` du SDK 11 avec `--xunit-version v3` : xUnit v3 sur Microsoft.Testing.Platform (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`)
  - ⚠️ Écart constaté sur la RC3 : le template `xunit` génère encore xUnit **v2** par défaut ; il faut passer `--xunit-version v3`
- ℹ️ Aspire 13.6 émet l'avertissement `ASPIRE010` quand l'AppHost est lancé sans la CLI Aspire (`AspireUseCliBundle=false`) ; supprimé dans le `.csproj` de l'AppHost pour garder un build sans warning

## 01 — Enregistrer une sonde
- 🆕 Validation async dans `System.ComponentModel.DataAnnotations` : `AsyncValidationAttribute` (`IsValidAsync`) pour le nom disponible (`ProbeNameAvailableAttribute`, registre avec latence simulée, casse ignorée, noms réservés), `IAsyncValidatableObject` (`ValidateAsync` → `IAsyncEnumerable<ValidationResult>`) pour la fenêtre de lancement vérifiée auprès de la station sol
  - ℹ️ Les deux types imposent aussi leur pendant synchrone (`IsValid` abstrait, `IValidatableObject.Validate`) : on les fait lever `NotSupportedException`
  - ℹ️ `Validator.ValidateObjectAsync` / `TryValidateObjectAsync` existent mais ne sont pas appelés directement : Minimal APIs et Blazor s'en chargent
- 🆕 Validation async de bout en bout dans les Minimal APIs : `POST /probes` renvoie 201 ou un problem details de validation, sans code de validation dans le handler (`AddProblemDetails` nécessaire pour obtenir `application/problem+json`, sinon `application/json`)
- 🆕 `EditForm` Blazor qui attend les validateurs async : « nom déjà pris » affiché pendant la frappe (`@bind:event="oninput"` + `NotifyFieldChanged`), chaque frappe annule la vérification précédente ; `EditContext.IsValidationPending(field)` / `IsValidationPending()` pilotent « Vérification auprès du registre… » et le bouton de soumission
- 🆕 Localisation intégrée des messages de validation (`AddLocalization` + `AddValidation(o => o.LocalizerProvider = ...)`) : `ErrorMessage` des attributs = clé dans `ValidationMessages.resx` (ressource neutre en français), partagée par l'API et le formulaire
  - ⚠️ Écart constaté sur la RC3 : seul l'`ErrorMessage` de l'attribut est localisé, et il remplace le message du `ValidationResult` renvoyé par `IsValidAsync` (un attribut = un message : « déjà pris » couvre donc aussi les noms réservés). Les messages produits par `IAsyncValidatableObject.ValidateAsync` ne sont pas localisés : on passe par `IStringLocalizer<ValidationMessages>` obtenu du `ValidationContext`

## 02 — Envoyer des commandes
- 🆕 C# 15 : closed hierarchies — `closed record ProbeCommand(Id, ProbeName)` → `Thrust`, `Rotate`, `CapturePhoto`, `SelfDestruct` ; le `switch` du centre de commandes et celui de la page n'ont pas de branche `default`
- 🆕 C# 15 : union types — `union CommandResult(Acknowledged, Rejected, TimedOut, ProbeLost)`, quatre records sans base commune ; switch exhaustif, y compris dans le corps de l'union (`CommandId`)
  - Démo : `git show` du commit « cas ProbeLost » — ajouter le cas fait échouer `CommandResult.CommandId` et `Commands.razor` avec CS8509 (warning promu en erreur par `TreatWarningsAsErrors`)
  - Union vs closed hierarchy : la commande est une famille qu'on possède (base commune, même assembly) ; le résultat compose des types indépendants
- 🆕 System.Text.Json : sérialisation/désérialisation des unions (cas actif écrit directement, sans enveloppe ni `$type` : `{"commandId":"…","reason":"…"}`)
- 🆕 `[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]` pour distinguer des cas objets d'après leurs propriétés (fonctionne même si tous les cas partagent `commandId`)
  - 🔁 En contraste, la commande en entrée reste un polymorphisme classique `[JsonPolymorphic]` / `[JsonDerivedType]` avec discriminant `kind` (rappel .NET 7+), compatible avec `closed`
- 🆕 Mini-bonus : `union SensorValue(double, string, bool)`, cas choisi d'après le type de token JSON
- 🆕 Minimal APIs : unions en body JSON, décrites en `anyOf` dans OpenAPI
  - `POST /commands` (`Results<Ok<CommandResult>, NotFound<ProblemDetails>>`) : réponse 200 = `anyOf` des quatre cas, sans `discriminator` ; la requête `ProbeCommand` apparaît aussi en `anyOf`, mais avec `discriminator` sur `kind`

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
- 🆕 LINQ `FullJoin` (`Enumerable`) : `GET /mission/report` et la page « Rapport » rapprochent les commandes envoyées des accusés de réception (`MissionReport`)
  - Côté non apparié = `default` : des records des deux côtés pour que `null` signifie « absent » ; ordre de sortie = commandes dans l'ordre, puis accusés orphelins à la fin
  - 🆕 C# 15 : classement par un `switch` exhaustif sur le tuple `(commande, accusé)` (`(null, null)` → `UnreachableException`) ; ce qui vaut accusé (`Acknowledged`, `Rejected`) se décide par un switch exhaustif sur l'union `CommandResult`
  - Scénario de démo : une photo (appariée), une rotation de 170° (expirée → commande sans accusé), et au premier contact chaque sonde envoie un accusé spontané (orphelin) ; une commande à une sonde autodétruite reste aussi sans accusé
- 🆕 `JsonSerializer.SerializeAsyncEnumerable` vers un `PipeWriter`, sortie JSON Lines (`topLevelValues: true`)

## 06 — « Houston, on a un problème »
- 🆕 Runtime Async : stack traces propres, moins d'allocations (comparaison net10.0 / net11.0 + BenchmarkDotNet)

## Bonus en rafale
- 🆕 `EqualityComparer<T>.Create` (comparateur depuis un sélecteur de clé)
- 🆕 C# 15 : `break` / `continue` labellisés
- 🆕 `Process` : helpers run-and-capture
- 🆕 File-based apps : `#:include`, `dotnet run -e`
- 🆕 Templates de tests en xUnit v3 sur Microsoft.Testing.Platform, affichage live de `dotnet test`
