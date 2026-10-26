# Product

## Register

product

## Users

Le speaker d'un meetup .NET 11 et son public. L'application est pilotée en direct sur scène et projetée sur grand écran, dans une salle à lumière tamisée : le public lit les écrans à 5–15 m, le speaker enchaîne les pages au fil des chapitres du talk.

## Product Purpose

Application fil rouge d'un talk sur les nouveautés de .NET 11 : un centre de contrôle de mission où l'on enregistre des sondes, leur envoie des commandes, ingère leur télémétrie, compare les formats au banc d'essai et produit un rapport de mission. Chaque page illustre une fonctionnalité du framework ; l'interface doit rendre la démo lisible et crédible, sans voler la vedette au code.

## Brand Personality

Précis, sobre, cinématographique. À la croisée des consoles SpaceX (noir profond, typographie DIN, épure), de la NASA (rigueur des documents de mission, procédures) et d'Ariane (l'orange de l'allumage, l'ingénierie européenne). On doit sentir une salle de contrôle, pas un jeu vidéo.

## Anti-references

- Sci-fi « HUD » néon : cyan sur noir partout, glow, scanlines, hexagones.
- Dashboards SaaS génériques : cartes arrondies à ombre douce, dégradés violets, gros chiffres décoratifs.
- Emojis comme iconographie dans les titres.

## Design Principles

- **Lisible à 10 mètres** : contraste fort, chiffres en chasse fixe, hiérarchie évidente au premier coup d'œil projeté.
- **L'état d'abord** : la couleur sert à dire nominal / attention / échec / en direct, jamais à décorer.
- **Une seule couleur d'allumage** : l'orange est réservé à l'action principale et aux moments de lancement.
- **La démo reste au premier plan** : chaque page garde son contenu et sa logique ; l'habillage ne complique pas le code montré.

## Accessibility & Inclusion

WCAG 2.2 AA : contraste ≥ 4,5:1 pour le texte, focus clavier visible, statuts jamais portés par la seule couleur (libellé toujours présent), `prefers-reduced-motion` respecté.
