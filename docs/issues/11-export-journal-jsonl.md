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
