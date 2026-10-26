# 11 — Export du journal de bord en JSONL

Labels : `ready-for-agent` · Tag : `05-reconcile` (posé à la fin des issues 10 et 11)

## Parent

[docs/spec.md](../spec.md)

## What to build

L'opérateur exporte le journal de bord via `GET /logbook/export` (et un lien de téléchargement dans l'UI). La réponse `application/jsonl` est diffusée en flux : `JsonSerializer.SerializeAsyncEnumerable` écrit directement dans le `PipeWriter` de la réponse avec `topLevelValues: true`. Chaque entrée est un document JSON compact terminé par LF, sans tableau englobant. L'export commence immédiatement, sans construire le fichier complet en mémoire.

## Acceptance criteria

- [ ] `GET /logbook/export` renvoie `application/jsonl`.
- [ ] Test via l'API : une ligne JSON valide par entrée, chaque ligne terminée par LF (y compris la dernière), aucun tableau englobant.
- [ ] L'export d'un gros journal ne fait pas croître la mémoire de l'API (vérification manuelle).
- [ ] Le tag `05-reconcile` est posé (après l'issue 10 et après `04-telemetry`).

## Blocked by

- [05 — Télémétrie JSONL (tracer bullet)](05-telemetrie-jsonl-tracer.md)

User stories : 56, 57, 58

## Mise à jour après code review

Constat : l'export ne relisait que l'enregistrement de début de vol gardé en mémoire (les 4096 premiers paquets de chaque sonde). Après l'import du million de paquets du simulateur, il n'en contenait qu'une petite partie : la démo de streaming n'avait plus de sens.

Décision (option C) : un journal de bord persistant sur disque.

- Pendant l'import, `TelemetryImporter` ajoute chaque lot (celui qui part au store), quel que soit le format, à un fichier JSONL append-only (`CrewLogbook`), avec le contrat `TelemetryJsonContext`. Le fichier vit dans un sous-dossier propre à l'instance, sous le dossier temporaire du système ou sous `Logbook:Directory`, supprimé à l'arrêt ; le dossier d'une instance tuée est supprimé au démarrage suivant.
- `GET /logbook/export` relit le fichier en flux (🔁 `JsonSerializer.DeserializeAsyncEnumerable(..., topLevelValues: true)`, .NET 10) et le réécrit avec 🆕 `JsonSerializer.SerializeAsyncEnumerable(Response.BodyWriter, ..., topLevelValues: true)`. Le journal n'est jamais entièrement en mémoire.
- Un export lancé pendant un import lit le fichier jusqu'à la longueur des lots complets écrits au moment où il commence : uniquement des lignes entières.
- L'ordre est celui de la réception (avant : sonde par sonde) ; chaque réception est consignée, retransmissions comprises (avant : dédupliquées par l'enregistrement de début de vol). Les paquets postés un par un (`POST /telemetry/packets`) ne vont pas au journal.
- L'enregistrement de début de vol reste en mémoire pour `packets[from, to]` et n'alimente plus l'export.
- Banc d'essai : chaque import paie l'écriture du journal, pour les 6 combinaisons ; la page le signale et le tableau de `docs/nouveautes-dotnet-11.md` a été remesuré.

Tests via l'API : N paquets importés en JSONL et en CBOR → exactement N lignes dans l'ordre de réception ; export pendant un import → lignes entières, préfixe de ce qui a été envoyé ; export vide → body vide en `application/jsonl` ; aucun fichier laissé sur disque à l'arrêt ; journal d'une station tuée supprimé au démarrage.

Vérifié à la main (Release, HTTP/1.1, graine 42) :

| Journal | Fichier | Lignes exportées | Durée de l'export | Working set de l'API pendant l'export |
|---|---|---|---|---|
| 1 import jsonl d'1 million de paquets | 100,7 Mo | 1 000 000 | 2,1 s | 121 à 162 Mo (156 Mo avant) |
| 6 imports du banc (6 millions de paquets) | 604,4 Mo | 6 000 000 | 11,0 s | 131 à 245 Mo (244 Mo avant) |

Le working set ne croît pas pendant l'export, quelle que soit la taille du journal.