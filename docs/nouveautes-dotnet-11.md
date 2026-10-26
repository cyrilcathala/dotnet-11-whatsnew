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
- 🆕 C# 15 : collection expression arguments — les noms réservés du registre : `HashSet<string> ReservedNames = [with(StringComparer.OrdinalIgnoreCase), "Voyager", "Pioneer", .. HistoricMissions]` (`with(...)` en premier élément, arguments passés au constructeur)
- 🆕 C# 15 : extension indexers — `extension(IEnumerable<ProbeCommand> history) { public ProbeCommand? this[Guid commandId] }` ; utilisé par `GET /commands/{id}` (`history.Commands[id]`, 404 si inconnue)
  - ℹ️ Pas de conflit avec l'indexeur `this[int]` de `IReadOnlyList<T>` : le type du paramètre (`Guid`) diffère

## 04 — Pluie de télémétrie (import JSON / JSONL / CBOR)
- 🆕 `ZstandardCompressedContent` (compression du body de requête côté `HttpClient`) : simulateur `--compression zstd`, compresse au fil de l'envoi, pose `Content-Encoding: zstd` et conserve le `Content-Type` du contenu enveloppé
- 🆕 `ZstandardStream` dans `System.IO.Compression` (aperçu d'un body zstd non déclaré, compression dans les tests)
- 🆕 Décompression zstd native côté ASP.NET Core : `AddRequestDecompression()` enregistre `zstd`, `br`, `deflate` et `gzip` (vérifié sur la RC3), aucun `IDecompressionProvider` maison n'est nécessaire. Le middleware retire l'en-tête `Content-Encoding` une fois le body décompressé
  - ⚠️ Un `Content-Encoding` inconnu (ou plusieurs encodages enchaînés) n'est pas rejeté par le middleware, qui laisse passer le body tel quel. L'endpoint repère l'en-tête restant et répond 415. Un body zstd corrompu lève `InvalidDataException` pendant la lecture : on répond 400.
  - ⚠️ La limite de taille du body (30 Mo) s'applique au body **décompressé**, et le middleware la lit avant l'endpoint : elle est levée par métadonnée (`DisableRequestSizeLimitAttribute`) sur `POST /telemetry/import`. Vérifié à la main : 1 million de paquets JSONL en zstd (≈ 100 Mo décompressés) importés en 2,7 s (Release, HTTP/1.1)
- 🆕 `CborReader` incrémental (`isFinalBlock: false` + `SlideData`)
  - `CborTelemetryFormat` : `CborReader` en `Lax` (obligatoire avec `isFinalBlock: false`), `AllowMultipleRootLevelValues`, une map par paquet. Seuls les paquets complets sont décodés (`TrySkipValue` renvoie `false` sans bouger sur un paquet coupé, état `NeedsMoreData`) ; on rend au `PipeReader` ce qui est consommé, puis `SlideData` sur le tampon suivant, qui commence par les octets non lus. Dernier bloc : `isFinalBlock: true`
  - Chaque paquet complet est relu par un second `CborReader` réutilisé via `Reset(slice, isFinalBlock: true)`
  - ⚠️ Lire un élément tronqué (`ReadTextString()` en `NeedsMoreData`) lève `CborContentException` : on ne décode pas champ par champ à travers les morceaux
  - Test : body écrit en morceaux de 1 à 37 octets avec flush entre chaque = même état qu'en un seul bloc, et même état qu'en JSONL
  - Vérifié à la main (Release, HTTP/1.1) : working set de l'API 150 Mo (200 000 paquets), 152 Mo (1 million), 177 Mo (5 millions) ; ~420 000 à 665 000 paquets/s
  - `System.Formats.Cbor` est dans le framework partagé ASP.NET Core ; la bibliothèque partagée et le simulateur ont besoin du package `System.Formats.Cbor` 11.0.0-rc.3
- 🆕 C# 15 : `union SensorValue(double, string, bool)` dans les paquets (cas JSON choisi d'après le type de token, sans convertisseur ; en CBOR, entier/flottant → `double`, texte → `string`, booléen → `bool`)
  - ⚠️ Écart : avec les options JSON « web » (Minimal APIs, `NumberHandling = AllowReadingFromString`), `"ok"` est ambigu entre `double` et `string`. `[JsonNumberHandling(JsonNumberHandling.Strict)]` posé sur l'union suffit (vérifié via `POST /telemetry/packets`), pas besoin de toucher aux options globales
  - Une union peut redéfinir `ToString()` dans son corps (affichage du tableau de bord)
- 🆕 Stream adapters sans copie : `ReadOnlySequenceStream`, `ReadOnlyMemoryStream`, `WritableMemoryStream`, `StringStream`
  - `ReadOnlySequenceStream` sur le chemin d'import (`UndeclaredZstd`) : un body zstd envoyé sans `Content-Encoding` est repéré à son numéro magique ; le tampon du `PipeReader` (jusqu'à 160 Kio, éventuellement en plusieurs segments) est passé sans copie à `ZstandardStream`, qui attend un `Stream`, pour citer le début du contenu dans le 415
  - `StringStream` (body JSONL texte encodé à la volée) et `ReadOnlyMemoryStream` (octets à compresser) dans les tests zstd
  - ℹ️ `WritableMemoryStream` n'est pas utilisé : aucun endroit naturel dans l'application
- 🆕 `System.IO.Pipelines` : moins de contention lecteur/écrivain (interne)
- 🔁 `JsonSerializer.DeserializeAsync(PipeReader)` / `Request.BodyReader`
  - JSONL : `JsonSerializer.DeserializeAsyncEnumerable(Request.BodyReader, typeInfo, topLevelValues: true)` lit un paquet par ligne, sans buffer le payload (rappel .NET 10, `JsonLinesTelemetryFormat`)
  - Vérifié à la main (Release, HTTP/1.1, profil http) : working set de l'API entre 120 et 145 Mo pour 200 000, 1 million (≈ 100 Mo de JSONL) et 5 millions de paquets
  - ⚠️ `UseStatusCodePagesWithReExecute` ré-exécute une réponse 415 sans body vers `/not-found` en POST, ce qui donne un 400 (antiforgery Blazor) : le 415 est donc renvoyé en problem details. Pour la même raison, la métadonnée `.Accepts(...)` (415 produit par le routage, sans body) n'est pas utilisée.
  - JSON (catalogue d'astéroïdes) : `JsonSerializer.DeserializeAsync(Request.BodyReader, typeInfo)` désérialise le document entier de façon asynchrone, sans passer par un `Stream` (rappel .NET 10, `AsteroidCatalogJsonFormat`). Le document porte un en-tête et la liste des astéroïdes (`GET /telemetry/catalog`) en plus des relevés, importés comme ceux des autres formats
  - Point de comparaison volontaire : un seul document n'est exploitable qu'une fois lu en entier, la mémoire suit donc la taille du payload. Mesuré à la main (Debug, HTTP/1.1) : pic de working set de l'API à 345 Mo pour 1 million de paquets en JSON, contre 120 à 145 Mo en JSONL
  - Test via l'API : le même jeu de paquets importé dans chaque format de `TelemetryFormats.All` produit le même état que JSONL (compteurs par sonde, dernière valeur, enregistrement du début de vol)
  - ⚠️ HTTP/2 n'est négocié qu'en HTTPS : avec le profil `http` (certificat de dev non approuvé), le simulateur retombe en HTTP/1.1. En HTTPS, `UseHttpsRedirection` doit être évité côté simulateur (un 307 sur un body en flux coupe la connexion), d'où l'utilisation directe de l'URL HTTPS fournie par Aspire.
- 🔁 C# 15 en rappel du chapitre 03 : tampon de lot `List<TelemetryPacket> batch = [with(capacity: BatchSize)]` dans l'ingestion, extension indexer `packets[from, to]` pour « les 5 premières minutes de vol » (tableau de bord et `GET /telemetry/probes/{probe}/launch`)
- ℹ️ Aspire : le simulateur est une ressource `WithExplicitStart()`, lancée depuis le dashboard
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
