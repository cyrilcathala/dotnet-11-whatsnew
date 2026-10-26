# Mission Control 🚀 — quoi de neuf dans .NET 11

Application fil rouge d'un talk meetup sur .NET 11 : un centre de contrôle de mission spatiale où l'on enregistre des sondes, leur envoie des commandes, ingère leur télémétrie et produit un rapport de mission.

Chaque chapitre du talk correspond à un tag git : on peut rejouer la démo étape par étape.

## Prérequis

- SDK .NET 11 RC3 (`11.0.100-rc.3.26522.148`) ou plus récent : la version est épinglée dans [`global.json`](global.json) avec `rollForward: latestFeature`.
- SDK .NET 10 (`10.0.403`) pour le chapitre « Houston » (comparaison .NET 10 / .NET 11).
- Aucun outil Aspire à installer : l'AppHost récupère le dashboard via NuGet au premier build.
- Optionnel : `dotnet dev-certs https --trust` pour le profil HTTPS.

## Lancer l'application

Une seule commande démarre l'API et le dashboard Aspire :

```bash
dotnet run --project src/MissionControl.AppHost
```

La console affiche l'URL de connexion au dashboard (`Login URL: ...`). Depuis le dashboard, la ressource `api` donne accès à l'application web, à ses logs, ses traces et ses métriques.

Sans certificat de développement, utilisez le profil HTTP :

```bash
dotnet run --project src/MissionControl.AppHost --launch-profile http
```

### Avec `dotnet watch`

🆕 .NET 11 : `dotnet watch` fonctionne directement sur l'AppHost et recharge à chaud les projets qu'il orchestre.

```bash
dotnet watch --project src/MissionControl.AppHost
```

Modifiez par exemple `src/MissionControl.Api/Components/Pages/Home.razor` : la modification est appliquée sans redémarrer.

### Envoyer de la télémétrie

La ressource `simulator` du dashboard Aspire ne démarre pas toute seule : cliquez sur ▶ pour envoyer un million de paquets JSONL à l'API, puis suivez l'import sur la page **Télémétrie** (`/telemetrie`) de l'application web.

Le simulateur se lance aussi à la main :

```bash
dotnet run --project tools/ProbeSimulator -- --format jsonl --compression none --count 1000000 --seed 42 --url http://localhost:5011
dotnet run --project tools/ProbeSimulator -- --format cbor --url http://localhost:5011
```

`--format json` envoie à la place le catalogue d'astéroïdes, un seul document JSON qui embarque les mêmes relevés ; le catalogue importé se consulte via `GET /telemetry/catalog`.

Avec `--compression zstd`, le body est compressé au fil de l'envoi (`Content-Encoding: zstd`) et l'API le décompresse avant de le lire, quel que soit le format.

### Lancer les tests

```bash
dotnet test
```

Les tests (xUnit v3 sur Microsoft.Testing.Platform) démarrent l'API en mémoire avec `WebApplicationFactory` et l'interrogent avec un vrai `HttpClient`.

## Structure

```
MissionControl.slnx
├─ src/MissionControl.AppHost          Aspire : orchestre l'application, dashboard
├─ src/MissionControl.ServiceDefaults  OpenTelemetry, health checks, résilience
├─ src/MissionControl.Api              Minimal API + Blazor Interactive Server, store en mémoire
├─ src/MissionControl.Telemetry        formats de télémétrie partagés (paquet, lecteurs et écrivains)
├─ tools/ProbeSimulator                console : génère et envoie la télémétrie des sondes
└─ tests/MissionControl.Tests          xUnit v3 (Microsoft.Testing.Platform)
```

Documents : [conception](docs/conception.md) · [spécification](docs/spec.md) · [nouveautés .NET 11 démontrées](docs/nouveautes-dotnet-11.md).

## Passer d'un chapitre à l'autre

```bash
git tag --list            # liste des chapitres
git switch --detach 02-commands
```

| Tag | Chapitre |
|---|---|
| `00-launch` | Lancement : AppHost, dashboard Aspire, `dotnet watch` |
| `01-register-probe` | Enregistrer une sonde : formulaire Blazor, validation async |
| `02-commands` | Envoyer des commandes : unions et closed hierarchies C# 15 |
| `03-csharp-sugar` | Petits raffinements C# 15 |
| `04-telemetry` | Pluie de télémétrie : JSON / JSONL / CBOR, zstd, banc d'essai |
| `05-reconcile` | Rapport de mission : `FullJoin`, export JSONL |
| `06-houston` | « Houston, on a un problème » : Runtime Async |
| `07-labeled-break` | Bonus : `break` / `continue` labellisés |
| `08-equality-comparer` | Bonus : `EqualityComparer<T>.Create` |
| `09-process` | Bonus : helpers `Process` |
| `10-file-based-apps` | Bonus : file-based apps |

Pour revenir à la dernière version : `git switch main`.
