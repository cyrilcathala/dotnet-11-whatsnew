# 07 — Catalogue d'astéroïdes en JSON

Labels : `ready-for-agent` · Tag : `04-telemetry`

## Parent

[docs/spec.md](../spec.md)

## What to build

L'opérateur importe le catalogue d'astéroïdes, un export classique sous la forme d'un seul gros document JSON, via le même endpoint `POST /telemetry/import` avec `Content-Type: application/json`. L'API le désérialise de façon asynchrone directement depuis le `PipeReader` (rappel .NET 10, à présenter comme tel). Le simulateur sait produire ce catalogue (`--format json`). Ce format sert de point de comparaison avec JSONL et CBOR dans le banc d'essai.

## Acceptance criteria

- [ ] `--format json` envoie un catalogue d'astéroïdes et l'API l'importe correctement.
- [ ] Le catalogue importé est consultable (dans l'UI ou via l'API).
- [ ] Test via l'API : le même jeu de données importé en JSON, JSONL et CBOR produit un état identique, pour les données communes aux trois formats.
- [ ] `docs/nouveautes-dotnet-11.md` marque ce chemin 🔁 (rappel .NET 10).

## Blocked by

- [05 — Télémétrie JSONL (tracer bullet)](05-telemetrie-jsonl-tracer.md)

User stories : 38
