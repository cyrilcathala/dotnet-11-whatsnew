# 06 — Télémétrie CBOR incrémentale + union `SensorValue`

Labels : `ready-for-agent` · Tag : `04-telemetry`

Statut : ✅ terminé — fusionné dans `integration/mission-control`

## Parent

[docs/spec.md](../spec.md)

## What to build

Le simulateur peut envoyer la télémétrie des sondes en CBOR (`--format cbor`, `Content-Type: application/cbor`). L'API la lit au fil de l'arrivée des données : `CborReader` en mode `Lax`, construit avec `isFinalBlock: false`. Sur `NeedsMoreData`, on avance le `PipeReader` et on fait glisser les nouvelles données avec `SlideData` ; le dernier bloc est passé avec `isFinalBlock: true`. Un paquet coupé entre deux morceaux réseau est reconstitué correctement, sans jamais buffer le payload complet.

Les valeurs de capteurs des paquets sont modélisées par `union SensorValue(double, string, bool)`. En JSON/JSONL, STJ choisit le cas d'après le type de token, sans configuration ; en CBOR, la bibliothèque de formats fait la correspondance avec les types CBOR équivalents.

## Acceptance criteria

- [ ] `--format cbor` importe correctement les paquets, et le tableau de bord se met à jour.
- [ ] Le même jeu de données importé en JSONL et en CBOR produit un état identique (test via l'API).
- [ ] Test via l'API : un `HttpContent` de test écrit le body CBOR en morceaux de taille aléatoire, avec un flush entre chaque, et le résultat est identique à un envoi en un seul bloc.
- [ ] Les valeurs `SensorValue` (nombre, texte, booléen) font l'aller-retour en JSON sans configuration STJ particulière.
- [ ] La mémoire de l'API reste plate en CBOR (vérification manuelle avec environ 1 million de paquets).

## Blocked by

- [05 — Télémétrie JSONL (tracer bullet)](05-telemetrie-jsonl-tracer.md)

User stories : 30, 36, 42
