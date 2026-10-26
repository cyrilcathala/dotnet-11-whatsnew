# 12 — Houston : Runtime Async

Labels : `ready-for-agent` · Tag : `06-houston`

Statut : ✅ terminé — fusionné dans `integration/mission-control`

## Parent

[docs/spec.md](../spec.md)

## What to build

Un outil console autonome, multi-ciblé `net10.0;net11.0`, qui ne dépend d'aucun projet utilisant C# 15. Il reproduit un pipeline async inspiré de l'ingestion de télémétrie, sur 4 à 5 niveaux d'`await`, avec une panne injectée au plus profond. Lancé avec `-f net10.0` puis `-f net11.0` côte à côte, il montre une stack trace encombrée de frames de state machine en .NET 10, et une stack trace limitée aux méthodes métier en .NET 11.

Un benchmark BenchmarkDotNet (`MemoryDiagnoser`, un job par runtime) mesure temps et allocations du même code async. Il est exécuté à l'avance, et ses résultats sont versionnés dans le dépôt pour être montrés pendant le talk.

## Acceptance criteria

- [ ] `dotnet run -f net10.0` et `dotnet run -f net11.0` affichent chacun la stack trace de la panne, avec une différence nettement visible.
- [ ] Le benchmark s'exécute pour les deux runtimes, et ses résultats (markdown) sont versionnés.
- [ ] L'outil compile avec le SDK 11 pour les deux cibles (le runtime .NET 10 est installé).
- [ ] Le README explique comment rejouer la comparaison et le benchmark.
- [ ] Le tag `06-houston` est posé (après `05-reconcile`).

## Blocked by

- [01 — Squelette de mission](01-squelette-de-mission.md)

User stories : 59, 60, 61, 62
