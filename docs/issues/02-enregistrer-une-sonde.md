# 02 — Enregistrer une sonde (validation async)

Labels : `ready-for-agent` · Tag : `01-register-probe`

Statut : ✅ terminé — fusionné dans `integration/mission-control`

## Parent

[docs/spec.md](../spec.md)

## What to build

L'opérateur enregistre une nouvelle sonde depuis un formulaire Blazor ou via `POST /probes`. Les deux passent par les mêmes règles asynchrones, qui s'exécutent pendant la saisie dans le formulaire :
- **Nom disponible** : le registre des sondes répond de façon asynchrone, avec une latence simulée visible. La comparaison ignore la casse, et une liste de noms réservés (« Voyager », « Pioneer »…) est refusée. Règle portée par un `AsyncValidationAttribute`.
- **Fenêtre de lancement disponible** : règle multi-champs portée par `IAsyncValidatableObject`, qui interroge de façon asynchrone une station sol simulée.

Les messages de validation sont en français, via la localisation intégrée des Minimal APIs (`AddLocalization` + `AddValidation`). L'API renvoie 201 ou un problem details de validation. La sonde enregistrée apparaît dans la flotte.

Le `HashSet` des noms réservés est construit ici de façon classique (`new HashSet<string>(StringComparer.OrdinalIgnoreCase)`) : il sera réécrit en C# 15 dans l'issue 04.

## Acceptance criteria

- [ ] Le formulaire affiche « nom déjà pris » pendant la saisie, après la latence simulée, sans soumission.
- [ ] Un nom existant avec une casse différente est refusé (formulaire et API).
- [ ] Un nom réservé est refusé (formulaire et API).
- [ ] Une fenêtre de lancement indisponible est refusée avec un message en français.
- [ ] `POST /probes` valide renvoie 201 et la sonde est visible dans la flotte.
- [ ] `POST /probes` invalide renvoie un problem details de validation avec des messages en français.
- [ ] Tests via l'API HTTP : nom pris (casse différente), nom réservé, fenêtre indisponible, enregistrement réussi.
- [ ] `docs/nouveautes-dotnet-11.md` reste à jour pour le chapitre 01.
- [ ] Le tag `01-register-probe` est posé.

## Blocked by

- [01 — Squelette de mission](01-squelette-de-mission.md)

User stories : 12, 13, 14, 15, 16, 17, 18, 19, 20, 21
