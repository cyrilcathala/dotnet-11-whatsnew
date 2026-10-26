# 10 — Rapport de mission (`FullJoin`)

Labels : `ready-for-agent` · Tag : `05-reconcile` (partagé avec 11)

## Parent

[docs/spec.md](../spec.md)

## What to build

L'opérateur consulte un rapport de mission (page Blazor « Rapport » et `GET /mission/report`) qui rapproche les commandes envoyées des accusés de réception grâce au nouvel opérateur LINQ `FullJoin`. Chaque ligne est classée dans l'une de ces catégories :
- **appariée** : la commande a son accusé ;
- **commande sans accusé** : commande perdue dans l'espace ;
- **accusé orphelin** : la sonde « parle sans qu'on lui ait rien demandé ».

Le simulateur ou le centre de commandes doit pouvoir produire les trois situations (accusés perdus, accusés spontanés) pour que la démo les montre toutes.

## Acceptance criteria

- [ ] Le rapport utilise `FullJoin` (`Enumerable` ou `AsyncEnumerable`).
- [ ] Les trois catégories apparaissent dans la page et dans la réponse de l'API pour un scénario de démo.
- [ ] Le classement des lignes passe par un `switch` exhaustif.
- [ ] Test via l'API : un scénario construit via l'API fait apparaître chaque catégorie avec les bons éléments.

## Blocked by

- [03 — Envoyer des commandes](03-envoyer-des-commandes.md)

Note : le tag `05-reconcile` n'est posé qu'après les issues 10 et 11, et après le tag `04-telemetry`.

User stories : 52, 53, 54, 55
