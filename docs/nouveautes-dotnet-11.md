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
  - `CborReader` veut une mémoire contiguë : le tampon du pipe est lu segment par segment, sans copie. Seul un paquet coupé en fin de segment est recopié dans un tampon loué, avec le début du segment suivant. ⚠️ `SlideData` refuse une fenêtre plus courte que les octets non lus (`ArgumentException`) : quand le paquet coupé déborde du segment courant, on recoud
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
- Banc d'essai : octets réseau, octets du payload, allocations, pic de working set, CPU, durée et débit par format × compression
  - Décorateur `InstrumentedTelemetryImporter` autour d'`ITelemetryImporter` ; mesures renvoyées dans la réponse de `POST /telemetry/import` (`measurements`), page « Banc d'essai » (`/banc-d-essai`) et `GET /telemetry/bench`
  - Octets réseau : middleware `UseImportTraffic()` placé **avant** `UseRequestDecompression()`, qui enveloppe le body compressé d'un flux compteur et note le `Content-Encoding` (retiré ensuite par le middleware natif). Octets payload : `PipeReader` compteur passé au lecteur de format, après décompression. Un body non compressé n'est pas enveloppé (le `PipeReader` natif de Kestrel est conservé) : octets réseau = octets payload
  - Métriques `System.Diagnostics.Metrics` (meter `MissionControl.Telemetry`, histogrammes `mission_control.import.*` tagués `format` et `compression`), exportées vers le dashboard Aspire par `AddMeter("MissionControl.*")`
  - Simulateur `--format all --compression both` : les 6 combinaisons en séquence, même graine (ressource Aspire `bench`)
  - Mesuré à la main (Release, HTTP/1.1, 1 million de paquets, graine 42, écriture du journal de bord comprise) :

    | Format | Compression | Réseau | Payload | Taux | Allocations | Pic WS | CPU | Durée | Débit (paquets/s) |
    |---|---|---|---|---|---|---|---|---|---|
    | json | none | 100,7 Mo | 100,7 Mo | 100 % | 374,1 Mo | 391,8 Mo | 4,64 s | 3,28 s | 305 013 |
    | json | zstd | 9,9 Mo | 100,7 Mo | 10 % | 370,6 Mo | 465,7 Mo | 2,17 s | 1,44 s | 693 547 |
    | jsonl | none | 100,7 Mo | 100,7 Mo | 100 % | 354,1 Mo | 387,3 Mo | 2,22 s | 1,33 s | 749 310 |
    | jsonl | zstd | 9,9 Mo | 100,7 Mo | 10 % | 353,9 Mo | 308,9 Mo | 1,50 s | 1,26 s | 790 847 |
    | cbor | none | 79,4 Mo | 79,4 Mo | 100 % | 425,7 Mo | 302,1 Mo | 2,64 s | 1,97 s | 506 991 |
    | cbor | zstd | 10,4 Mo | 79,4 Mo | 13 % | 425,4 Mo | 294,3 Mo | 2,02 s | 1,67 s | 599 233 |

  - ℹ️ Chaque import écrit aussi ses paquets dans le journal de bord sur disque (chapitre 05) : même coût pour les 6 lignes, la comparaison reste équitable, mais les valeurs absolues incluent cette écriture (les mesures précédentes, sans journal, donnaient par exemple jsonl/none en 0,97 s et 1 029 870 paquets/s ; 1,33 s et 749 310 paquets/s avec)
  - ⚠️ Mesures sur tout le processus : le pic de working set d'un import hérite du tas laissé par le précédent (le GC ne rend pas la mémoire tout de suite), seul le premier import part d'un processus « froid ». Le premier import (json) paie aussi le JIT

## 05 — Rapport de mission
- 🆕 LINQ `FullJoin` (`Enumerable`) : `GET /mission/report` et la page « Rapport » rapprochent les commandes envoyées des accusés de réception (`MissionReport`)
  - Côté non apparié = `default` : des records des deux côtés pour que `null` signifie « absent » ; ordre de sortie = commandes dans l'ordre, puis accusés orphelins à la fin
  - Classement par un `switch` exhaustif sur le tuple `(commande, accusé)` (`(null, null)` → `UnreachableException`) ; ce qui vaut accusé (`Acknowledged`, `Rejected`) se décide par un switch exhaustif sur l'union `CommandResult` (🆕 C# 15, rappel du chapitre 02)
  - Scénario de démo : une photo (appariée), une rotation de 170° (expirée → commande sans accusé), et au premier contact chaque sonde envoie un accusé spontané (orphelin) ; une commande à une sonde autodétruite reste aussi sans accusé
- 🆕 `JsonSerializer.SerializeAsyncEnumerable` vers un `PipeWriter`, sortie JSON Lines (`topLevelValues: true`)
  - `GET /logbook/export` (`LogbookEndpoints`) : `SerializeAsyncEnumerable(Response.BodyWriter, logbook.ReadAsync(ct), typeInfo, topLevelValues: true, ct)` → `application/jsonl`, un document compact par entrée terminé par LF (y compris la dernière), sans tableau englobant ; vérifié par test
  - ℹ️ Le journal de bord = tous les paquets reçus par import, dans l'ordre de réception, ajoutés lot par lot (quel que soit le format) à un fichier JSONL append-only dans un dossier temporaire propre à l'instance (`CrewLogbook`), supprimé à l'arrêt : même contrat JSON (`TelemetryJsonContext`) que l'import, l'export se réimporte tel quel. Après un import d'1 million de paquets, l'export contient bien 1 million de lignes (avant, seulement l'enregistrement de début de vol : 4096 paquets par sonde)
  - 🔁 Rappel .NET 10 : l'export relit le fichier en flux avec `JsonSerializer.DeserializeAsyncEnumerable(stream, typeInfo, topLevelValues: true, ct)` et le réécrit aussitôt avec `SerializeAsyncEnumerable` : le journal n'est jamais entièrement en mémoire
  - ℹ️ Export pendant un import : l'export lit le fichier jusqu'à la longueur des lots complets connue à son début (chaque lot est écrit et vidé sous verrou) ; il ne contient que des lignes entières
  - ℹ️ `POST /telemetry/packets` (un relevé isolé) alimente le store mais pas le journal, réservé aux imports
  - ℹ️ La surcharge `PipeWriter` ne complète pas le writer : inutile ici, ASP.NET Core complète `Response.BodyWriter` à la fin de la requête

## 06 — « Houston, on a un problème »
- 🆕 Runtime Async : plus de state machine générée par le compilateur, la pile d'appels réelle ne contient plus que les méthodes métier (21 frames en .NET 10, 9 en .NET 11 dans `tools/Houston`)
- 🆕 Runtime Async : moins d'allocations et plus rapide sur le même pipeline (BenchmarkDotNet ShortRun, 100 paquets : 38,7 µs / 28 928 B en .NET 10 → 21,9 µs / 144 B en .NET 11)
  - ⚠️ Écart constaté sur la RC3 : Runtime Async n'est pas actif par défaut pour le code utilisateur ; il faut `<Features>$(Features);runtime-async=on</Features>` (posé pour `net11.0` seulement, sinon le runtime .NET 10 refuse de charger les méthodes)
  - ⚠️ `Exception.ToString()` est déjà nettoyé depuis .NET Core 2.1 et reste identique : la différence se montre avec la pile réelle (`new StackTrace()`) capturée au moment de la panne
  - ℹ️ BenchmarkDotNet 0.15.8 ne connaît pas .NET 11 : `0.16.0-preview.2` (`RuntimeMoniker.Net11_0`)

## Bonus en rafale
- 🆕 `EqualityComparer<T>.Create` (comparateur depuis un sélecteur de clé)
- 🆕 C# 15 : `break` / `continue` labellisés (tag `07-labeled-break`) — la station sol propose la prochaine fenêtre de lancement libre quand celle demandée est prise (`GroundStation.FindNextFreeWindow`) : la fenêtre revient chaque jour à la même heure, `nextDay: for (...)` sur les jours, `foreach` sur la flotte, `continue nextDay;` dès qu'une sonde occupe le créneau, sans drapeau booléen ni `goto`
  - Visible dans le message de validation de `POST /probes` et du formulaire : « … Prochaine fenêtre libre : 2030-01-09 01:00 → 2030-01-09 07:00. »
- 🆕 `Process` : helpers run-and-capture
- 🆕 File-based apps : `#:include`, `dotnet run -e`
- 🆕 Templates de tests en xUnit v3 sur Microsoft.Testing.Platform, affichage live de `dotnet test`
