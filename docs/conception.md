# Mission Control 🚀 — document de conception

Application fil rouge pour un talk meetup de 45 min sur .NET 11. Décisions issues de la séance de questions du 25/10/2026.

## Cadre

- Talk de 45 min, public de devs .NET qui connaissent déjà .NET 10.
- Code préparé à l'avance, **un tag git par chapitre**, pas de live-coding risqué.
- Meetup **après la GA** de .NET 11 (mi-novembre 2026) : développement sur la RC3, puis bascule et répétition complète sur la GA.
- `global.json` épinglé sur la RC3 (`rollForward: latestFeature`), puis sur la GA.
- Code en anglais ; UI, messages de validation, README et commentaires (rares) en français.
- Liste des nouveautés démontrées : [nouveautes-dotnet-11.md](nouveautes-dotnet-11.md) (alimente le slide récapitulatif).

## Domaine

Centre de contrôle de mission spatiale : on enregistre des sondes, on leur envoie des commandes, on ingère leur télémétrie et on produit un rapport de mission.

## Architecture

```
MissionControl.slnx
├─ src/MissionControl.AppHost      Aspire : orchestre l'API et le simulateur, dashboard = support visuel
├─ src/MissionControl.ServiceDefaults
├─ src/MissionControl.Api          Minimal API + Blazor Interactive Server, store en mémoire
├─ tools/ProbeSimulator            console : génère et envoie la télémétrie (JSON / JSONL / CBOR, zstd)
├─ tools/Houston                   console multi-ciblée net10.0;net11.0 : comparaison Runtime Async + BenchmarkDotNet
└─ tests/MissionControl.Tests      xUnit v3 (Microsoft.Testing.Platform), filet de sécurité RC3 → GA
```

- Pas d'EF Core, pas de base de données : store en mémoire.
- Le code de l'API est 100 % net11.0. Seul `Houston` multi-cible.

## Déroulé et tags

| Tag | Chapitre | Contenu | Durée |
|---|---|---|---|
| `00-launch` | Lancement | AppHost, dashboard Aspire, `dotnet watch` + Aspire | 3 min |
| `01-register-probe` | Enregistrer une sonde | Formulaire Blazor + `POST /probes`, validation async, messages localisés | 6 min |
| `02-commands` | Envoyer des commandes | Closed hierarchy `ProbeCommand`, union `CommandResult`, STJ, OpenAPI `anyOf` | 8 min |
| `03-csharp-sugar` | Petits raffinements | Collection expression arguments, extension indexers | 4 min |
| `04-telemetry` | Pluie de télémétrie | Import streaming JSON/JSONL/CBOR, zstd, stream adapters, banc d'essai | 9 min |
| `05-reconcile` | Rapport de mission | `FullJoin` commandes × accusés, export du journal de bord en JSONL via PipeWriter | 6 min |
| `06-houston` | « Houston, on a un problème » | Runtime Async : stack traces 10 vs 11, benchmark pré-calculé | 5 min |
| — | Bonus et conclusion | `EqualityComparer.Create`, `break`/`continue` labellisés, `Process`, file-based apps, xUnit v3 | 4 min |

## Détails par chapitre

### 01 — Validation async
- Nom de sonde unique, vérifié dans le registre de façon asynchrone (latence simulée) et sans tenir compte de la casse (`HashSet` créé avec `[with(StringComparer.OrdinalIgnoreCase), ..]` au chapitre 03).
- *Par défaut :* `IAsyncValidatableObject` pour une règle multi-champs : la fenêtre de lancement demandée doit être disponible auprès de la station sol.
- Même modèle validé côté Minimal API (`POST /probes`) et côté `EditForm` Blazor.

### 02 — Unions et closed hierarchies
- `closed record ProbeCommand` → `Thrust`, `Rotate`, `CapturePhoto`, `SelfDestruct` : une famille qu'on possède, switch exhaustif sans `default`.
- `union CommandResult(Acknowledged, Rejected, TimedOut)` : des types indépendants combinés au cas par cas. En démo, on ajoute `ProbeLost` et le compilateur signale les switch incomplets.
- STJ avec `JsonUnionTypeStructuralClassifier` ; `anyOf` dans OpenAPI.
- Mini-bonus : `union SensorValue(double, string, bool)`.

### 03 — Sucre C# 15
- `HashSet<string> reserved = [with(StringComparer.OrdinalIgnoreCase), "Voyager", "Pioneer", .. existingNames];`
- `List<TelemetryPacket> batch = [with(capacity: BatchSize)];`
- Extension indexers : `history[commandId]` sur `IEnumerable<ProbeCommand>`, `packets[from, to]` sur `IReadOnlyList<TelemetryPacket>`.
- Ordre des chapitres conservé (option a) : seuls les noms réservés et `history[commandId]` sont dans ce tag. Le tampon `[with(capacity)]` et `packets[from, to]` arrivent au chapitre 04 et y sont présentés en rappel.
- Bonus : un tag chacun — `07-labeled-break`, `08-equality-comparer`, `09-process`, `10-file-based-apps`.

### 04 — Télémétrie
- Simulateur : `IAsyncEnumerable<TelemetryPacket>` → sérialisation au fil de l'eau → `ZstandardCompressedContent` → HTTP/2.
- `POST /telemetry/import`, format choisi par `Content-Type` (`application/json`, `application/jsonl`, `application/cbor`).
- Serveur : décompression zstd (❓ native ou `IDecompressionProvider` maison avec `ZstandardStream`) → `Request.BodyReader` → lecteur du format concerné. CBOR lu de façon incrémentale (`SlideData`). `ReadOnlySequenceStream` là où une API attend un `Stream`.
- Sources : catalogue d'astéroïdes (JSON, un seul gros document), journal de bord (JSONL), télémétrie des sondes (CBOR).
- Banc d'essai, à chaque requête : octets réseau (avant décompression), octets du payload (après), allocations (`GC.GetTotalAllocatedBytes(true)`), pic de working set, CPU (`Process.TotalProcessorTime`), durée et débit.
  - Métriques OTel taguées `format` et `compression`, visibles dans Aspire.
  - Page Blazor « Banc d'essai » : tableau format × compression (6 lignes).
  - Simulateur `--format all --compression both`, même graine, imports **en séquence** (les mesures portent sur tout le processus, à dire au public).
- *Par défaut :* environ 1 million de paquets pour la démo de mémoire plate, réglable par `--count`.

### 05 — Réconciliation et export
- `FullJoin` commandes envoyées × accusés de réception : exécutées, perdues, accusés orphelins (la sonde « parle sans qu'on lui ait rien demandé »).
- `GET /logbook/export` : `JsonSerializer.SerializeAsyncEnumerable(Response.BodyWriter, ..., topLevelValues: true)` → JSONL.

### 06 — Runtime Async
- `tools/Houston` : panne injectée à 4 ou 5 niveaux d'`await`, `dotnet run -f net10.0` et `-f net11.0` côte à côte.
- BenchmarkDotNet (`MemoryDiagnoser`, jobs Net10_0 / Net11_0) exécuté à l'avance ; on ne montre que les résultats.

### Tests
- CBOR découpé en morceaux de taille aléatoire = même résultat qu'en un seul bloc.
- Aller-retour STJ de `CommandResult` pour chaque cas.
- Réconciliation `FullJoin` : chaque catégorie.
- Même jeu de données importé en JSON, JSONL et CBOR = résultats identiques.

## Prérequis machine
- SDK .NET 11 RC3 installé (11.0.100-rc.3.26522.148) ; garder le SDK 10.0.403 pour `Houston`.
- Aspire (workload / templates) dans la version compatible .NET 11.
