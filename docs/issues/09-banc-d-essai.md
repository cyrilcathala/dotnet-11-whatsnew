# 09 — Banc d'essai format × compression

Labels : `ready-for-agent` · Tag : `04-telemetry` (posé à la fin de cette issue)

## Parent

[docs/spec.md](../spec.md)

## What to build

Chaque import est mesuré et les résultats sont comparables d'un format à l'autre. L'instrumentation enveloppe l'ingestion et mesure pour chaque requête :
- les octets reçus sur le réseau (compteur avant décompression) et les octets du payload (compteur après décompression) ;
- la mémoire allouée (delta de `GC.GetTotalAllocatedBytes(precise: true)`) ;
- le pic de working set (échantillonnage périodique pendant l'import) ;
- le temps CPU (delta de `Process.TotalProcessorTime`) ;
- la durée et le débit en paquets par seconde.

Les mesures sont publiées en métriques OpenTelemetry (`System.Diagnostics.Metrics`, histogrammes tagués `format` et `compression`), visibles dans le dashboard Aspire, et conservées pour une page Blazor « Banc d'essai » qui affiche un tableau de 6 lignes (JSON, JSONL, CBOR, chacun sans compression et en zstd). Le simulateur gagne `--format all --compression both`, qui enchaîne les 6 combinaisons **en séquence** avec la même graine. La page indique que les mesures portent sur tout le processus.

## Acceptance criteria

- [ ] `--format all --compression both` produit les 6 lignes du tableau en une commande.
- [ ] Les métriques taguées `format` et `compression` apparaissent dans le dashboard Aspire.
- [ ] Le taux de compression (octets réseau / octets payload) est affiché.
- [ ] La page signale que les mesures portent sur tout le processus et que les imports sont séquentiels.
- [ ] La réponse de `POST /telemetry/import` contient le résumé des mesures.
- [ ] Les valeurs numériques ne sont pas testées ; la présence du résumé dans la réponse l'est.
- [ ] Le tag `04-telemetry` est posé (après les issues 05 à 08 et après `03-csharp-sugar`).

## Blocked by

- [06 — Télémétrie CBOR incrémentale](06-telemetrie-cbor-incrementale.md)
- [07 — Catalogue d'astéroïdes en JSON](07-catalogue-asteroides-json.md)
- [08 — Compression zstd de bout en bout](08-compression-zstd.md)

User stories : 45, 46, 47, 48, 49, 51
