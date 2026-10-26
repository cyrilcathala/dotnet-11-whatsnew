# 04 — Raffinements C# 15

Labels : `ready-for-agent` · Tag : `03-csharp-sugar`

## Parent

[docs/spec.md](../spec.md)

## What to build

Réécrire en C# 15 du code déjà vu aux chapitres 01 et 02, pour montrer un avant/après sur du code familier au public :
- **Collection expression arguments** : l'ensemble des noms réservés du registre devient une seule expression de collection avec son comparateur insensible à la casse, plus un spread des noms existants (forme `[with(StringComparer.OrdinalIgnoreCase), "Voyager", "Pioneer", .. existingNames]`).
- **Extension indexer** sur un type qu'on ne possède pas : retrouver une commande de l'historique par son identifiant (`history[commandId]` sur `IEnumerable<ProbeCommand>`), utilisé dans l'API ou l'UI des commandes.

Option (a) retenue : l'indexeur temporel `packets[from, to]` et le tampon `[with(capacity)]` sont introduits plus tard, dans l'issue 05 (chapitre `04-telemetry`), et présentés en rappel.

Le comportement observable ne change pas : c'est un refactoring démonstratif.

## Acceptance criteria

- [ ] Les noms réservés sont construits avec une collection expression utilisant `with(...)`.
- [ ] La recherche d'une commande par identifiant passe par un extension indexer et est utilisée par une vraie fonctionnalité (détail d'une commande, par exemple).
- [ ] Tous les tests existants passent sans modification.
- [ ] La différence avec le tag `02-commands` se limite à ces réécritures (diff lisible en présentation).
- [ ] Le tag `03-csharp-sugar` est posé.

## Blocked by

- [03 — Envoyer des commandes](03-envoyer-des-commandes.md)

User stories : 31, 33, 35
