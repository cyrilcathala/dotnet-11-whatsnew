# 05 — Télémétrie JSONL (tracer bullet)

Labels : `ready-for-agent` · Tag : `04-telemetry` (partagé avec 06, 07, 08, 09)

Statut : ✅ terminé — fusionné dans `integration/mission-control`

## Parent

[docs/spec.md](../spec.md)

## What to build

Le premier chemin complet de la télémétrie, en JSON Lines et sans compression : le simulateur de sondes, orchestré par Aspire, génère un flux de paquets (graine fixe, volume réglable, environ 1 million par défaut) et les envoie en HTTP/2 sur `POST /telemetry/import` avec `Content-Type: application/jsonl`. L'API lit le body via `Request.BodyReader` (`PipeReader`) élément par élément, sans buffering du payload complet, et alimente le store. Le tableau de bord Blazor affiche en temps réel le nombre de paquets reçus, le débit et les sondes actives.

Cette tranche pose les fondations réutilisées par les issues 06 à 09 :
- une **bibliothèque de formats partagée** entre le simulateur, l'API et les tests (modèle de paquet, écrivains et lecteurs) ;
- dans l'API, une interface étroite « lire un flux de paquets depuis un `PipeReader` », choisie d'après le `Content-Type` ;
- dans le simulateur, des options `--format`, `--compression`, `--count`, `--seed` (seul `jsonl` + `none` est implémenté ici).

C# 15 en rappel (option (a) de l'issue 04) :
- le tampon de lot de l'ingestion est créé avec `[with(capacity: BatchSize)]` ;
- un extension indexer à deux bornes `packets[from, to]` sur `IReadOnlyList<TelemetryPacket>` extrait une fenêtre temporelle, utilisée par une vraie fonctionnalité (par exemple « les 5 premières minutes de vol » d'une sonde).

## Acceptance criteria

- [ ] Lancer le simulateur depuis Aspire importe les paquets en JSONL et le tableau de bord se met à jour en temps réel.
- [ ] Le working set de l'API ne croît pas avec la taille du payload (vérifié manuellement avec environ 1 million de paquets).
- [ ] Un `Content-Type` non supporté renvoie 415.
- [ ] Le tampon de lot utilise `[with(capacity: ...)]` ; l'indexeur `packets[from, to]` est utilisé par une fonctionnalité visible.
- [ ] Tests via l'API HTTP : import JSONL d'un jeu de données connu, puis état observé via l'API conforme.
- [ ] `docs/nouveautes-dotnet-11.md` signale la lecture STJ via `PipeReader` comme rappel .NET 10.

## Blocked by

- [01 — Squelette de mission](01-squelette-de-mission.md)

Note : le tag `04-telemetry` n'est posé qu'après l'issue 09, et après le tag `03-csharp-sugar` (issue 04).

User stories : 32, 34, 37, 39, 41, 43, 44
