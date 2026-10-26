# Houston — « on a un problème » 🚀

Outil console du chapitre `06-houston` : le même code async, exécuté sous .NET 10 puis sous .NET 11, pour montrer ce que change **Runtime Async**.

- `TelemetryPipeline.cs` : pipeline d'ingestion sur 5 niveaux d'`await` (`IngestBatchAsync` → `DecodePacketAsync` → `ValidateReadingAsync` → `ReadSensorAsync` → `CalibrateAsync`). La panne est injectée au plus profond, et la pile d'appels réelle est capturée au moment de la panne.
- `TelemetryPipelineBenchmarks.cs` : benchmark BenchmarkDotNet du même pipeline sans panne (`MemoryDiagnoser`, un job court par runtime).

Le projet est multi-ciblé `net10.0;net11.0` et ne référence aucun autre projet de la solution. Runtime Async est activé pour la cible `net11.0` uniquement, dans [`Directory.Build.targets`](Directory.Build.targets).

## Prérequis

- SDK .NET 11 (épinglé par le `global.json` du dépôt), qui compile les deux cibles.
- Runtime .NET 10 installé (fourni par le SDK `10.0.403`) pour exécuter la cible `net10.0`.

## Rejouer la comparaison des stack traces

Depuis la racine du dépôt :

```bash
dotnet run --project tools/Houston -f net10.0
dotnet run --project tools/Houston -f net11.0
```

En .NET 10, chaque niveau d'`await` apparaît deux fois, séparé par `AsyncMethodBuilderCore.Start`, et la reprise sur le pool de threads ajoute les frames `AsyncStateMachineBox` / `ExecutionContext` : 21 frames. En .NET 11, la pile ne contient plus que les méthodes métier, puis le pool de threads : 9 frames.

## Rejouer le benchmark

```bash
dotnet run --project tools/Houston -c Release -f net11.0 -- bench --filter "*"
```

BenchmarkDotNet compile et lance un processus par runtime (.NET 10 et .NET 11). Le rapport est écrit dans `BenchmarkDotNet.Artifacts/results/` (ignoré par git) ; copiez le fichier `*-report-github.md` dans [`results/`](results/) pour mettre à jour les résultats versionnés.

Résultats pré-calculés : [`results/Houston.TelemetryPipelineBenchmarks-report-github.md`](results/Houston.TelemetryPipelineBenchmarks-report-github.md).

## Remarques

- Runtime Async n'est pas encore activé par défaut sur la RC3 : sans `<Features>runtime-async=on</Features>`, le compilateur génère toujours des state machines, même en `net11.0`.
- BenchmarkDotNet 0.15.8 ne connaît pas `RuntimeMoniker.Net11_0` : on utilise `0.16.0-preview.2`.
- `ToString()` de l'exception est déjà nettoyé depuis .NET Core 2.1 (les frames `MoveNext` y sont masquées) et reste identique dans les deux runtimes ; la différence se voit dans la pile réelle (`new StackTrace()`), celle que voient un profileur, un dump ou le débogueur.
