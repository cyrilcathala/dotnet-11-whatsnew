# 13 — Bonus en rafale

Labels : `ready-for-agent` · Tags : `07-labeled-break`, `08-equality-comparer`, `09-process`, `10-file-based-apps`

## Parent

[docs/spec.md](../spec.md)

## What to build

Quatre petites démonstrations, chacune dans son propre commit avec son propre tag, dans cet ordre. Chacune simplifie réellement du code existant ou apporte un vrai outil, sans exemple jouet :
1. **`07-labeled-break`** : remplacer une boucle imbriquée à drapeau booléen, ou un `goto`, par un `break` ou `continue` labellisé (par exemple dans le parcours des paquets ou la recherche d'une fenêtre de lancement).
2. **`08-equality-comparer`** : remplacer un comparateur écrit à la main par `EqualityComparer<T>.Create` avec un sélecteur de clé (par exemple pour dédupliquer des paquets ou des sondes).
3. **`09-process`** : utiliser les nouveaux helpers `Process` de type run-and-capture dans un outil de la mission (par exemple un diagnostic qui interroge `dotnet --info`, ou le lancement du simulateur).
4. **`10-file-based-apps`** : un script C# sans `.csproj` en plusieurs fichiers via `#:include` (par exemple un outil de diagnostic de mission qui interroge l'API), lancé avec `dotnet run -e` pour passer une variable d'environnement.

En fin d'issue, vérifier que `docs/nouveautes-dotnet-11.md` reflète exactement ce qui a été démontré, prêt pour le slide récapitulatif.

## Acceptance criteria

- [ ] Quatre commits distincts, tagués `07-labeled-break`, `08-equality-comparer`, `09-process`, `10-file-based-apps`, posés après `06-houston`.
- [ ] Chaque diff est court et lisible en présentation.
- [ ] Tous les tests passent à chaque tag.
- [ ] Le script file-based s'exécute avec `dotnet run` et `-e`.
- [ ] `docs/nouveautes-dotnet-11.md` est à jour et ne contient plus aucun ❓.

## Blocked by

- [10 — Rapport de mission](10-rapport-de-mission.md)
- [11 — Export du journal de bord en JSONL](11-export-journal-jsonl.md)
- [12 — Houston : Runtime Async](12-houston-runtime-async.md)

User stories : 6
