# 01 — Squelette de mission

Labels : `ready-for-agent` · Tag : `00-launch`

Statut : ✅ terminé — fusionné dans `integration/mission-control`

## Parent

[docs/spec.md](../spec.md)

## What to build

Le squelette de bout en bout de Mission Control : une seule commande démarre l'AppHost Aspire, qui lance l'application web ; on y voit une page d'accueil Blazor (Interactive Server) en français, et le dashboard Aspire montre traces, logs et métriques de l'API. Le dépôt est initialisé, le SDK .NET 11 épinglé, et un projet de tests xUnit v3 vérifie à travers l'API HTTP en process (`WebApplicationFactory`) que l'application répond. Ce squelette établit le seam de test unique utilisé par toutes les tranches suivantes.

Contenu :
- Dépôt git initialisé, `.gitignore`, solution `.slnx`.
- SDK épinglé sur la RC3 de .NET 11 avec `rollForward: latestFeature`.
- AppHost Aspire, ServiceDefaults, application web (Minimal API + Blazor Interactive Server), projet de tests (template xUnit v3 du SDK 11, Microsoft.Testing.Platform).
- Store en mémoire injecté en singleton (vide à ce stade).
- README en français : prérequis, lancement de l'AppHost, `dotnet watch` sur l'AppHost, liste des tags.

## Acceptance criteria

- [ ] Lancer l'AppHost démarre l'application web, visible et en bonne santé dans le dashboard Aspire.
- [ ] `dotnet watch` sur l'AppHost fonctionne et recharge une modification de l'API.
- [ ] La page d'accueil Blazor s'affiche en français et est interactive.
- [ ] Le document OpenAPI de l'API est exposé.
- [ ] `dotnet test` exécute au moins un test passant via `WebApplicationFactory` et un vrai `HttpClient`.
- [ ] Le code de l'API cible uniquement `net11.0`, sans `LangVersion` preview.
- [ ] Le README explique comment lancer l'application et passer d'un tag à l'autre.
- [ ] Le tag `00-launch` est posé.

## Blocked by

None - can start immediately

User stories : 1, 2, 3, 4, 5, 9, 11, 63, 64, 65
