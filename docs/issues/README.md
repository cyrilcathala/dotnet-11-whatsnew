# Issues — Mission Control 🚀

Issues locales en attendant un tracker (à migrer sur GitHub). Toutes sont `ready-for-agent`. Source : [spec.md](../spec.md).

**Règle des tags** : chaque tag contient tout le code des tags précédents. Le code de plusieurs issues peut avancer en parallèle, mais un tag n'est posé qu'une fois toutes les issues de son chapitre terminées *et* le tag précédent posé.

| # | Issue | Bloquée par | Tag |
|---|---|---|---|
| 01 | [Squelette de mission](01-squelette-de-mission.md) | — | `00-launch` |
| 02 | [Enregistrer une sonde](02-enregistrer-une-sonde.md) | 01 | `01-register-probe` |
| 03 | [Envoyer des commandes](03-envoyer-des-commandes.md) | 02 | `02-commands` |
| 04 | [Raffinements C# 15](04-raffinements-csharp-15.md) | 03 | `03-csharp-sugar` |
| 05 | [Télémétrie JSONL (tracer bullet)](05-telemetrie-jsonl-tracer.md) | 01 | `04-telemetry` |
| 06 | [Télémétrie CBOR incrémentale](06-telemetrie-cbor-incrementale.md) | 05 | `04-telemetry` |
| 07 | [Catalogue d'astéroïdes en JSON](07-catalogue-asteroides-json.md) | 05 | `04-telemetry` |
| 08 | [Compression zstd de bout en bout](08-compression-zstd.md) | 05 | `04-telemetry` |
| 09 | [Banc d'essai](09-banc-d-essai.md) | 06, 07, 08 | `04-telemetry` |
| 10 | [Rapport de mission](10-rapport-de-mission.md) | 03 | `05-reconcile` |
| 11 | [Export du journal de bord en JSONL](11-export-journal-jsonl.md) | 05 | `05-reconcile` |
| 12 | [Houston : Runtime Async](12-houston-runtime-async.md) | 01 | `06-houston` |
| 13 | [Bonus en rafale](13-bonus-en-rafale.md) | 10, 11, 12 | `07-labeled-break`, `08-equality-comparer`, `09-process`, `10-file-based-apps` |
