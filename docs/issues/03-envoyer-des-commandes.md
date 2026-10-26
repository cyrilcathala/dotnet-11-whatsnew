# 03 — Envoyer des commandes (unions + closed hierarchies)

Labels : `ready-for-agent` · Tag : `02-commands`

## Parent

[docs/spec.md](../spec.md)

## What to build

L'opérateur envoie une commande à une sonde enregistrée depuis une page Blazor « Commandes » ou via `POST /commands`, puis voit le résultat. Le centre de commandes simule la réponse de la sonde et conserve l'historique des commandes et des accusés de réception.

Forme des types (décidée lors de la conception) :
- `ProbeCommand` est une **closed hierarchy**. La racine porte l'identifiant de commande et le nom de la sonde ; les cas sont `Thrust` (delta-V), `Rotate` (attitude cible), `CapturePhoto` (cible) et `SelfDestruct` (code de confirmation). Les `switch` sur les commandes n'ont pas de branche `default`.
- `CommandResult` est une **union** `union CommandResult(Acknowledged, Rejected, TimedOut)` de trois records indépendants, sans base commune. La sérialisation JSON écrit le cas actif sans enveloppe ni `$type`, avec `[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]`.
- Le document OpenAPI décrit la réponse de `POST /commands` comme un `anyOf`.

Pour la démo, préparer le passage où l'on ajoute un cas `ProbeLost` à l'union et où le compilateur signale les `switch` incomplets. Ce cas fait partie de l'état final du tag.

## Acceptance criteria

- [ ] Les quatre commandes peuvent être envoyées depuis l'UI et via l'API à une sonde enregistrée.
- [ ] Une commande vers une sonde inconnue est rejetée proprement.
- [ ] Le résultat affiché distingue chaque cas de l'union.
- [ ] Aucun `switch` sur `ProbeCommand` ou `CommandResult` n'a de branche `default`.
- [ ] Le JSON d'un `CommandResult` ne contient ni enveloppe ni `$type`.
- [ ] Le document OpenAPI expose un `anyOf` pour la réponse de `POST /commands`.
- [ ] Tests via l'API HTTP : chaque cas de `CommandResult` fait l'aller-retour JSON correctement côté client.
- [ ] L'historique des commandes et des accusés est conservé dans le store en mémoire (il sert à l'issue 10).
- [ ] Le tag `02-commands` est posé.

## Blocked by

- [02 — Enregistrer une sonde](02-enregistrer-une-sonde.md)

User stories : 22, 23, 24, 25, 26, 27, 28, 29
