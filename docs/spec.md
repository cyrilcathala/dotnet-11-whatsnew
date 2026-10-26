# Spec — Mission Control 🚀, application fil rouge .NET 11

> Statut : `ready-for-agent` (pas encore publiée sur un tracker ; à migrer en issue une fois le repo GitHub configuré).
> Documents liés : [conception.md](conception.md), [nouveautes-dotnet-11.md](nouveautes-dotnet-11.md).

## Problem Statement

Je présente les nouveautés de .NET 11 lors d'un meetup de 45 minutes, devant des développeurs .NET qui connaissent déjà .NET 10. Les démos de nouveautés sont souvent une suite d'exemples isolés et artificiels (`Cat`, `Dog`, `Foo`) qui n'expliquent pas *quand* et *pourquoi* utiliser chaque feature, et le live-coding sur une version toute fraîche du SDK est risqué. J'ai besoin d'une application unique, crédible et ludique, qui fasse apparaître chaque nouveauté au moment où elle répond à un vrai besoin, que je peux dérouler étape par étape sans risque, et dont je peux montrer l'impact mesurable (mémoire, CPU, octets réseau, stack traces).

## Solution

Une application de **centre de contrôle de mission spatiale** : on enregistre des sondes, on leur envoie des commandes, on ingère massivement leur télémétrie dans plusieurs formats, on produit un rapport de mission, puis une panne permet de montrer Runtime Async. L'application est orchestrée par Aspire (dashboard comme support visuel), expose une Minimal API et une UI Blazor en français, et s'accompagne d'un simulateur de sondes et d'un outil de comparaison .NET 10 / .NET 11. Le code est construit en chapitres successifs, un tag git par chapitre, chaque chapitre introduisant un groupe de nouveautés. Une liste tenue à jour des nouveautés démontrées alimente le slide récapitulatif.

## User Stories

### Présentateur — déroulé du talk
1. En tant que présentateur, je veux un tag git par chapitre, afin de passer d'une étape à l'autre sans live-coding risqué.
2. En tant que présentateur, je veux que chaque tag compile et démarre sans intervention, afin de ne jamais rester bloqué devant le public.
3. En tant que présentateur, je veux démarrer toute l'application avec une seule commande, afin d'ouvrir le talk en moins d'une minute.
4. En tant que présentateur, je veux le dashboard Aspire comme support visuel, afin de montrer traces, logs et métriques sans écrire d'UI dédiée.
5. En tant que présentateur, je veux utiliser `dotnet watch` sur l'AppHost Aspire, afin de montrer cette intégration du SDK 11 et de modifier du code en direct si besoin.
6. En tant que présentateur, je veux une liste des nouveautés démontrées, classées par chapitre et marquées nouveau / rappel .NET 10 / à confirmer, afin de construire un slide récapitulatif honnête.
7. En tant que présentateur, je veux que chaque chapitre tienne dans son minutage (3 à 9 minutes), afin de respecter les 45 minutes.
8. En tant que présentateur, je veux un domaine métier compris en quelques secondes, afin de ne pas perdre de temps à l'expliquer.
9. En tant que présentateur, je veux un code en anglais et une UI en français, afin d'être lisible sur GitHub tout en parlant au public francophone.
10. En tant que présentateur, je veux des commentaires rares signalant les nouveautés à l'écran, afin de guider le regard du public.
11. En tant que présentateur, je veux un README en français expliquant comment lancer chaque chapitre, afin que le public puisse rejouer la démo après le talk.

### Opérateur de mission — enregistrement des sondes (chapitre 01)
12. En tant qu'opérateur, je veux enregistrer une nouvelle sonde via un formulaire, afin de l'ajouter à la flotte.
13. En tant qu'opérateur, je veux être prévenu pendant la saisie qu'un nom de sonde est déjà pris, afin de ne pas soumettre un formulaire voué à l'échec.
14. En tant qu'opérateur, je veux que la vérification du nom ignore la casse, afin que « voyager » soit refusé si « Voyager » existe.
15. En tant qu'opérateur, je veux que les noms réservés (« Voyager », « Pioneer », etc.) soient refusés, afin de respecter l'historique.
16. En tant qu'opérateur, je veux que la fenêtre de lancement demandée soit vérifiée auprès de la station sol, afin de ne pas programmer un lancement impossible.
17. En tant qu'opérateur, je veux des messages de validation en français, afin de les comprendre immédiatement.
18. En tant qu'intégrateur, je veux enregistrer une sonde via `POST /probes`, afin d'automatiser l'enregistrement.
19. En tant qu'intégrateur, je veux que l'API applique exactement les mêmes règles asynchrones que le formulaire, afin d'avoir un comportement cohérent.
20. En tant qu'intégrateur, je veux recevoir une réponse de validation standard (problem details) avec des messages localisés, afin de les afficher tels quels.
21. En tant que présentateur, je veux que la vérification du nom ait une latence simulée visible, afin que le public perçoive le caractère asynchrone.

### Opérateur de mission — commandes (chapitre 02)
22. En tant qu'opérateur, je veux envoyer une commande de poussée, de rotation, de capture photo ou d'autodestruction à une sonde, afin de piloter la mission.
23. En tant qu'opérateur, je veux voir le résultat de chaque commande (exécutée, rejetée, expirée), afin de savoir si la sonde a obéi.
24. En tant qu'intégrateur, je veux envoyer une commande via `POST /commands`, afin de piloter la flotte par script.
25. En tant qu'intégrateur, je veux que le résultat soit sérialisé en JSON sans enveloppe ni discriminant `$type`, afin d'avoir un contrat simple.
26. En tant qu'intégrateur, je veux que le document OpenAPI décrive le résultat comme un `anyOf` de ses cas, afin de générer des clients corrects.
27. En tant que développeur dans le public, je veux voir le compilateur exiger un traitement exhaustif des commandes sans branche `default`, afin de comprendre l'intérêt des closed hierarchies.
28. En tant que développeur dans le public, je veux voir l'ajout d'un cas « sonde perdue » au résultat provoquer des erreurs dans tous les `switch` incomplets, afin de comprendre l'intérêt des unions.
29. En tant que développeur dans le public, je veux une explication par le code de la différence entre union et closed hierarchy, afin de savoir quand utiliser l'une ou l'autre.
30. En tant que développeur dans le public, je veux voir une valeur de capteur (nombre, texte ou booléen) désérialisée sans configuration selon le type de token JSON, afin de voir le comportement par défaut de STJ avec les unions.

### Développeur dans le public — raffinements C# 15 (chapitre 03)
31. En tant que développeur, je veux voir un ensemble insensible à la casse construit en une seule expression de collection avec son comparateur, afin d'apprécier les collection expression arguments.
32. En tant que développeur, je veux voir un tampon de lot créé avec sa capacité dans une expression de collection, afin de voir l'usage orienté performance.
33. En tant qu'opérateur, je veux retrouver une commande de l'historique par son identifiant via un indexeur, afin d'écrire un code de lecture naturel.
34. En tant qu'opérateur, je veux extraire les paquets de télémétrie d'une fenêtre temporelle via un indexeur à deux bornes, afin d'analyser une phase du vol.
35. En tant que développeur, je veux que ces indexeurs s'appliquent à des types que je ne possède pas, afin de comprendre l'intérêt des extension indexers.

### Opérateur de mission — télémétrie (chapitre 04)
36. En tant qu'opérateur, je veux importer la télémétrie des sondes au format CBOR, afin de traiter le format binaire compact qu'elles émettent.
37. En tant qu'opérateur, je veux importer le journal de bord de l'équipage au format JSON Lines, afin de traiter un flux ajouté ligne à ligne.
38. En tant qu'opérateur, je veux importer le catalogue d'astéroïdes au format JSON, afin de traiter un export classique d'un seul document.
39. En tant qu'intégrateur, je veux un endpoint d'import unique dont le format est choisi par `Content-Type`, afin de simplifier l'intégration.
40. En tant qu'intégrateur, je veux envoyer des payloads compressés en zstd, afin de réduire le volume réseau.
41. En tant qu'opérateur, je veux importer un million de paquets sans que la mémoire du serveur augmente avec la taille du payload, afin de supporter des volumes réels.
42. En tant qu'opérateur, je veux que la télémétrie CBOR soit traitée au fur et à mesure de son arrivée, même si un élément est coupé entre deux morceaux réseau, afin de ne jamais attendre la fin du payload.
43. En tant qu'opérateur, je veux un tableau de bord qui montre en temps réel le nombre de paquets reçus, le débit et les sondes actives, afin de suivre l'import.
44. En tant que présentateur, je veux un simulateur de sondes qui génère un volume réglable de paquets dans le format et la compression choisis, afin de produire la charge en direct.
45. En tant que présentateur, je veux que le simulateur utilise une graine fixe, afin que toutes les combinaisons format × compression reçoivent les mêmes données.
46. En tant que présentateur, je veux que le simulateur enchaîne toutes les combinaisons en séquence, afin d'obtenir un comparatif équitable en une commande.
47. En tant que présentateur, je veux mesurer pour chaque import les octets reçus sur le réseau, les octets du payload décompressé, la mémoire allouée, le pic de working set, le temps CPU, la durée et le débit, afin de comparer les formats.
48. En tant que présentateur, je veux ces mesures en métriques OpenTelemetry taguées par format et compression, afin qu'elles apparaissent dans Aspire sans code d'affichage.
49. En tant que présentateur, je veux une page « Banc d'essai » avec un tableau format × compression, afin de laisser le résultat comparatif à l'écran.
50. En tant que développeur dans le public, je veux voir un tampon segmenté passé sans copie à une API qui attend un `Stream`, afin de comprendre les nouveaux stream adapters.
51. En tant que développeur dans le public, je veux qu'on me dise clairement quelles briques sont nouvelles en .NET 11 et lesquelles datent de .NET 10, afin de ne pas être induit en erreur.

### Opérateur de mission — rapport et export (chapitre 05)
52. En tant qu'opérateur, je veux un rapport qui rapproche les commandes envoyées des accusés de réception, afin de savoir ce qui a réellement été exécuté.
53. En tant qu'opérateur, je veux voir les commandes sans accusé de réception, afin d'identifier les commandes perdues dans l'espace.
54. En tant qu'opérateur, je veux voir les accusés de réception sans commande correspondante, afin de détecter les sondes qui « parlent sans qu'on leur ait rien demandé ».
55. En tant qu'opérateur, je veux voir les commandes appariées à leur accusé, afin de confirmer leur exécution.
56. En tant qu'opérateur, je veux exporter le journal de bord en JSON Lines, afin de l'exploiter avec des outils ligne à ligne.
57. En tant qu'opérateur, je veux que l'export démarre immédiatement et se diffuse en flux, afin de ne pas attendre la construction complète du fichier.
58. En tant qu'intégrateur, je veux que chaque ligne exportée soit un document JSON compact terminé par un saut de ligne LF, afin de respecter le format JSONL canonique.

### Développeur dans le public — Runtime Async (chapitre 06)
59. En tant que développeur, je veux voir la même panne profonde dans une chaîne d'`await` exécutée sous .NET 10 puis sous .NET 11, afin de comparer les stack traces.
60. En tant que développeur, je veux que la stack trace .NET 11 ne montre que les méthodes métier, afin de constater le gain de diagnostic.
61. En tant que développeur, je veux voir un benchmark des allocations et du temps entre .NET 10 et .NET 11 sur le même code async, afin de constater le gain de performance.
62. En tant que présentateur, je veux des résultats de benchmark pré-calculés, afin de ne pas lancer un benchmark long en direct.

### Mainteneur — fiabilité RC3 → GA
63. En tant que mainteneur, je veux une suite de tests rapide sur les comportements qui reposent sur les API les plus jeunes, afin de détecter en quelques secondes une rupture lors du passage à la GA.
64. En tant que mainteneur, je veux une version de SDK épinglée, afin que le résultat de compilation soit reproductible jusqu'au jour du talk.
65. En tant que mainteneur, je veux basculer de la RC3 à la GA en changeant uniquement l'épinglage de version et les versions de packages, afin de limiter le travail de migration.

## Implementation Decisions

### Organisation de la solution
- Une solution au format `.slnx` comprenant : un AppHost Aspire, un projet de ServiceDefaults Aspire, l'application web (Minimal API + Blazor), un simulateur de sondes en console, un outil console de comparaison Runtime Async, un projet de tests.
- L'application web et le simulateur ciblent uniquement `net11.0` et utilisent C# 15 (version par défaut, sans `LangVersion` preview).
- Seul l'outil de comparaison Runtime Async est multi-ciblé `net10.0;net11.0` ; il ne dépend d'aucun autre projet utilisant C# 15.
- SDK épinglé sur la RC3 de .NET 11 avec `rollForward: latestFeature`, puis sur la GA. Le SDK .NET 10 reste installé pour l'outil de comparaison.
- Pas d'EF Core ni de base de données : un store en mémoire, thread-safe, injecté en singleton.
- Le code partagé entre le simulateur et l'application (modèles de paquets, écrivains et lecteurs de formats) vit dans une petite bibliothèque commune, afin que les tests et le simulateur exercent les mêmes contrats.

### Modules
- **Registre des sondes** : enregistre les sondes et répond de façon asynchrone « ce nom est-il disponible ? » (latence simulée), insensible à la casse, avec une liste de noms réservés.
- **Station sol** : répond de façon asynchrone sur la disponibilité d'une fenêtre de lancement.
- **Centre de commandes** : reçoit les commandes, simule la réponse des sondes, conserve l'historique des commandes et des accusés de réception.
- **Ingestion de télémétrie** : une interface étroite « lire un flux de paquets depuis un `PipeReader` » avec trois implémentations (JSON, JSONL, CBOR), choisies d'après le `Content-Type`.
- **Instrumentation d'import** : enveloppe l'ingestion, compte les octets avant et après décompression, mesure allocations, working set, CPU, durée et débit, publie des métriques OTel et conserve les derniers résultats pour la page « Banc d'essai ».
- **Rapport de mission** : rapproche commandes et accusés par un `FullJoin` et classe chaque ligne.
- **Journal de bord** : chaque lot de paquets importé (quel que soit le format) est ajouté, dans l'ordre de réception, à un fichier JSON Lines append-only sur disque, dans un dossier temporaire propre à l'instance de l'application (configurable, supprimé à l'arrêt ; celui d'une instance tuée est supprimé au démarrage suivant). L'enregistrement de début de vol (4096 premiers paquets par sonde) reste en mémoire pour la fenêtre temporelle `packets[from, to]`, mais n'alimente plus l'export.
- **Export du journal de bord** : relit le fichier du journal en flux (`DeserializeAsyncEnumerable`, `topLevelValues: true`) et le diffuse en JSONL directement dans le `PipeWriter` de la réponse ; le journal n'est jamais entièrement en mémoire. Un export lancé pendant un import s'arrête à la longueur des lots complets écrits au moment où il commence : uniquement des lignes entières.

### Modèle de domaine (forme des types)
- Commandes : une **closed hierarchy** dont la racine porte l'identifiant de la commande et le nom de la sonde ; cas `Thrust` (delta-V), `Rotate` (attitude cible), `CapturePhoto` (cible), `SelfDestruct` (code de confirmation).
- Résultat de commande : une **union** `CommandResult(Acknowledged, Rejected, TimedOut)` de trois records indépendants, sans base commune ; un quatrième cas `ProbeLost` est ajouté pendant la démo. La sérialisation JSON utilise le classifieur structurel de STJ (`JsonUnionTypeStructuralClassifier`), car les trois cas sont des objets.
- Valeur de capteur : une **union** `SensorValue(double, string, bool)`, sérialisée avec la sélection par défaut de STJ (selon le type de token).
- Classification du rapport : chaque ligne est appariée (commande + accusé), commande sans accusé, ou accusé orphelin.

### Contrats d'API
- `POST /probes` : enregistre une sonde (nom, fenêtre de lancement souhaitée, éventuellement d'autres caractéristiques). Validation asynchrone : nom disponible (`AsyncValidationAttribute`) et fenêtre de lancement disponible (`IAsyncValidatableObject`). Réponse 201 ou problem details de validation avec messages localisés en français (`AddLocalization` + `AddValidation`).
- `POST /commands` : accepte une commande de la closed hierarchy ; renvoie un `CommandResult` décrit en `anyOf` dans OpenAPI.
- `POST /telemetry/import` : body lu via `Request.BodyReader`. `Content-Type` parmi `application/json`, `application/jsonl`, `application/cbor` ; `Content-Encoding: zstd` optionnel. Renvoie un résumé de l'import (nombre de paquets et mesures).
- `GET /mission/report` : rapport de réconciliation commandes × accusés.
- `GET /logbook/export` : `application/jsonl`, diffusé en flux via `JsonSerializer.SerializeAsyncEnumerable(..., topLevelValues: true)` vers le `PipeWriter` de la réponse.

### Streaming et formats
- JSON (catalogue d'astéroïdes) : un seul gros document désérialisé de façon asynchrone depuis le `PipeReader` (rappel .NET 10).
- JSONL (journal de bord) : lu élément par élément depuis le `PipeReader`.
- CBOR (télémétrie) : `CborReader` en mode `Lax`, construit avec `isFinalBlock: false` ; sur `NeedsMoreData`, on avance le `PipeReader` et on fait glisser les nouvelles données via `SlideData` ; dernier bloc avec `isFinalBlock: true`. Aucun buffering du payload complet.
- Décompression zstd côté serveur : middleware `RequestDecompression`. Si ASP.NET Core 11 ne fournit pas de provider zstd, on écrit un `IDecompressionProvider` maison basé sur `ZstandardStream`. Point à confirmer en premier lors de l'implémentation du chapitre 04.
- Stream adapters : `ReadOnlySequenceStream` pour passer sans copie un segment du `PipeReader` à une API qui attend un `Stream` ; d'autres adapters (`ReadOnlyMemoryStream`, `StringStream`) utilisés là où ils s'appliquent naturellement (simulateur, tests).
- Simulateur : produit un `IAsyncEnumerable` de paquets, les sérialise au fil de l'eau et les envoie en HTTP/2 via `ZstandardCompressedContent` quand la compression est activée. Options : format (`cbor|jsonl|json|all`), compression (`none|zstd|both`), nombre de paquets (environ 1 million par défaut), graine.

### Mesures
- Octets réseau : compteur sur le body brut, avant décompression. Octets du payload : compteur après décompression.
- Allocations : delta de `GC.GetTotalAllocatedBytes(precise: true)`. Pic de working set : échantillonnage périodique pendant l'import. CPU : delta de `Process.TotalProcessorTime`.
- Les mesures portent sur tout le processus : le banc d'essai impose des imports séquentiels, et on le dit au public.
- Chaque import écrit aussi le journal de bord sur disque, quel que soit le format : le coût est le même pour les 6 combinaisons, la comparaison reste équitable, mais les valeurs absolues l'incluent. La page « Banc d'essai » le signale.
- Exposition via `System.Diagnostics.Metrics` (histogrammes tagués `format` et `compression`) et via un résumé consultable par la page « Banc d'essai ».

### UI
- Blazor en Interactive Server, partout.
- Pages : tableau de bord (compteurs en temps réel), enregistrement d'une sonde (`EditForm` avec validation asynchrone), commandes, rapport de mission, banc d'essai.

### C# 15 — usages imposés
- Collection expression arguments : ensemble des noms réservés insensible à la casse ; tampon de lot avec capacité dans l'ingestion.
- Extension indexers : recherche d'une commande par identifiant sur `IEnumerable<ProbeCommand>` ; fenêtre temporelle à deux bornes sur `IReadOnlyList<TelemetryPacket>`.
- Bonus : `break`/`continue` labellisés et `EqualityComparer<T>.Create` là où ils simplifient réellement du code existant.

### Runtime Async
- L'outil de comparaison réutilise un pipeline async de 4 à 5 niveaux avec une panne injectée au plus profond ; exécuté avec `-f net10.0` puis `-f net11.0`.
- Un benchmark BenchmarkDotNet (`MemoryDiagnoser`, un job par runtime) est exécuté à l'avance ; ses résultats sont versionnés pour être montrés pendant le talk.

### Chapitres et tags
- `00-launch`, `01-register-probe`, `02-commands`, `03-csharp-sugar`, `04-telemetry`, `05-reconcile`, `06-houston`, puis un tag par bonus : `07-labeled-break`, `08-equality-comparer`, `09-process`, `10-file-based-apps`. Chaque tag contient tout le code des chapitres précédents.
- Le chapitre `03-csharp-sugar` ne réécrit que du code des chapitres 01 et 02 (noms réservés, `history[commandId]`). L'indexeur temporel `packets[from, to]` et le tampon `[with(capacity)]` arrivent avec la télémétrie (`04-telemetry`) et sont présentés en rappel.

## Testing Decisions

- **Un bon test** vérifie un comportement observable de l'extérieur (requête HTTP → réponse et état exposé par l'API), jamais un détail d'implémentation (classe interne, méthode privée, structure du store). Un refactoring interne ne doit casser aucun test.
- **Seam unique** : l'application web hébergée en process (`WebApplicationFactory`), utilisée à travers un vrai `HttpClient`. Aucun autre seam n'est introduit.
- **Framework** : xUnit v3 sur Microsoft.Testing.Platform, avec le template par défaut du SDK 11.
- **Comportements testés** :
  - Le même jeu de données importé en JSON, JSONL et CBOR produit un état identique, observé via l'API.
  - Un body CBOR écrit par un `HttpContent` de test en morceaux de taille aléatoire, avec un flush entre chaque, donne le même résultat qu'un envoi en un seul bloc (cela couvre `NeedsMoreData` et `SlideData`).
  - Un import envoyé via `ZstandardCompressedContent` est décompressé et importé correctement.
  - `POST /commands` : chaque cas de `CommandResult` fait l'aller-retour JSON correctement côté client.
  - `POST /probes` : un nom déjà pris avec une casse différente, ou réservé, et une fenêtre de lancement indisponible renvoient des erreurs de validation en français.
  - `GET /mission/report` : chaque catégorie (appariée, commande sans accusé, accusé orphelin) apparaît pour un scénario construit via l'API.
  - `GET /logbook/export` : une ligne JSON valide par entrée, chaque ligne terminée par LF, aucun tableau englobant.
  - `GET /logbook/export` après un import de N paquets (JSONL et CBOR) : exactement N lignes, dans l'ordre de réception, réimportables ; body vide quand rien n'a été importé ; lignes entières pendant un import ; aucun fichier laissé sur disque à l'arrêt.
- **Non testés** : UI Blazor, simulateur (sauf sa bibliothèque de formats, exercée via l'API), outil de comparaison Runtime Async, valeurs numériques du banc d'essai.
- **Prior art** : aucun, le repo est vide. Ces tests établiront le modèle.
- **Rôle** : filet de sécurité pour la bascule RC3 → GA, lancé avant chaque répétition.

## Out of Scope

- EF Core, base de données, persistance durable (le fichier du journal de bord est temporaire : il disparaît avec l'instance).
- Authentification et autorisation.
- Déploiement, conteneurisation, CI/CD.
- Vector search, full-text search, composants Blazor AI, `Decimal32/64/128`, SIMD, cryptographie (ML-KEM, X25519), memory safety (preview).
- File-based apps au-delà d'une mention en bonus.
- Blazor WebAssembly et rendu static SSR.
- Mesures de performance isolées par requête (EventPipe, dotnet-counters) : les mesures sont au niveau du processus.
- Benchmark exécuté en direct pendant le talk.
- Tests de l'UI et tests de charge.

## Further Notes

- Le meetup a lieu après la GA de .NET 11 (mi-novembre 2026). Il faut prévoir une répétition complète sur la GA, car les API les plus jeunes (unions avec STJ, CBOR incrémental, Zstandard, `FullJoin`) peuvent changer entre la RC3 et la GA.
- La liste [nouveautes-dotnet-11.md](nouveautes-dotnet-11.md) doit être mise à jour à chaque ajout ou retrait d'une nouveauté démontrée : elle alimente le slide récapitulatif.
- Les nouveautés montrées mais datant de .NET 10 (désérialisation STJ depuis un `PipeReader`) doivent être présentées comme telles.
- Prérequis machine : SDK .NET 11 RC3 installé (11.0.100-rc.3.26522.148), SDK .NET 10 conservé, Aspire dans la version compatible .NET 11. Le dossier n'est pas encore un repo git : le `git init` et la création des tags font partie du chapitre `00-launch`.
