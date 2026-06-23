# Storyboard — Bande-annonce SettlersOfIdlestan

Document de référence créatif. À relire/modifier à chaque itération — ne pas le laisser dériver du
contenu réel de `TrailerDefinition.json`. Voir [TECH_PLAN.md](TECH_PLAN.md) pour la faisabilité technique.

## Pitch

Le gameplay se présente dès la première image — pas de carton logo en ouverture, juste un fondu rapide
sur l'île en train de tourner. Montage qui accélère progressivement, calé sur les piliers 4X —
**Explore / Expand / Exploit / Exterminate** — chaque section se refermant sur un aperçu de la
progression long terme qu'elle débloque (arbre de prestige après Expand, arbre de recherche après
Exploit). Puis bascule sur l'argument idle **"At your own pace"**, avant de plonger vers l'Inframonde et
de finir sur le reveal de l'Abyss Gate (question à l'écran), juste avant le titre. Cible : fans de 4X et
d'idle games.

## Découpage temporel (30s @ 30fps = 900 frames)

| # | Temps | Durée | Texte incrusté | Beat narratif | Action de jeu | Caméra | Vitesse sim |
|---|---|---|---|---|---|---|---|
| 0 | 0:00–0:01 | 1s | *(aucun)* | Fondu d'ouverture directement sur le gameplay | Île déjà visible, fondu depuis le noir/blanc | Plan fixe ou très léger zoom | x1 |
| 1 | 0:01–0:05 | 4s | "EXPLORE" | Découverte de la carte hexagonale procédurale | Révélation des tuiles, premiers outposts | Travelling panoramique sur toute l'île | x1–2 |
| 2a | 0:05–0:08.5 | 3.5s | "EXPAND" | Expansion du territoire | Autoplay Expand : routes + outposts posés en rafale | Travelling latéral qui suit la construction | x3–4 |
| 2b | 0:08.5–0:10 | 1.5s | "PRESTIGE" (discret) | Ce que l'expansion débloque sur le long terme | Coupe vers l'onglet Carte de Prestige, quelques sommets déjà acquis | Zoom in léger sur l'arbre | x1 |
| 3a | 0:10–0:13.5 | 3.5s | "EXPLOIT" | Exploitation des ressources | Autoplay Exploit : bâtiments de prod, récolte animée, entrepôts qui se remplissent | Zoom in sur 2–3 villes actives, coupes plus rapides | x3–4 |
| 3b | 0:13.5–0:15 | 1.5s | "RESEARCH" (discret) | Ce que l'exploitation débloque sur le long terme | Coupe vers l'onglet Recherche, plusieurs technologies déjà obtenues | Zoom in léger sur l'arbre | x1 |
| 4 | 0:15–0:19 | 4s | "EXTERMINATE" | Guerre contre une civilisation ennemie | Autoplay Exterminate : palissades, casernes construites près de la civ adverse, animations de combat | Zoom dynamique sur l'affrontement | x1–2 (combat lisible) |
| 5 | 0:19–0:23 | 4s | "AT YOUR OWN PACE" | Argument idle | Retour à l'île principale, complète et prospère, UI TimeControl visible (vitesse) | Zoom out lent, stabilisation calme | x1 (retour calme) |
| 6 | 0:23–0:28 | 5s | "THE UNDERWORLD AWAITS" puis, sur le visuel final de l'Abyss Gate : "Passerez-vous le portail des abysses ?" | Teaser Inframonde + reveal de la nouvelle feature | Plongée vers la couche Inframonde (changement de layer), puis plan final fixe sur l'Abyss Gate | Travelling vertical "descente", puis plan fixe qui tient sur le visuel final | x1–2 |
| 7 | 0:28–0:30 | 2s | Logo + nom du jeu | Reveal du titre | Carton de fin, plan fixe | — | — |

**Le trailer se termine sur le visuel de l'Abyss Gate** (beat 6) avec la question à l'écran, puis cut
direct sur le titre (beat 7) — pas de fondu lent, la coupe doit être nette pour l'effet "cliffhanger".

## Notes de montage

- Pas de logo/texte en ouverture : le jeu doit se vendre par l'image dès la frame 1. Le fondu d'entrée
  (#0) est un fondu d'image (depuis le noir), pas un carton de titre.
- Le nom du jeu n'apparaît qu'au tout dernier beat (7), seul sur un plan fixe — plus de combo
  "texte idle + logo" sur le même plan.
- Les textes de section ("EXPLORE", "EXPAND"...) en fondu (~0.3s in/out), affichés ~1.5–2s.
- Les inserts arbre de prestige / arbre de recherche (2b, 3b) sont volontairement courts et avec un
  texte plus discret ("PRESTIGE", "RESEARCH") — ce sont des piqûres de rappel "il y a une progression
  long terme derrière", pas des beats à part entière qui cassent le rythme Explore/Expand/Exploit/Exterminate.
- Rythme des coupes caméra qui s'accélère légèrement de EXPLORE à EXTERMINATE, se calme sur "At Your Own
  Pace" (contraste qui renforce le message idle), puis repart en tension descendante sur le teaser
  Inframonde/Abyss Gate (sensation de "profondeur"/mystère) jusqu'au plan final figé.
- Le plan final de l'Abyss Gate (fin du beat 6) doit tenir assez longtemps pour que la question à
  l'écran soit lisible avant le cut sec vers le titre — pas de fondu, une coupe franche.
- Le logo final (beat 7) reste visible au moins 1s sur plan fixe (lisible en vignette/preview).

## Abyss Gate — dépendance bloquante à suivre

L'Abyss Gate est une **nouvelle feature d'île** qui n'existe pas encore dans le jeu (pas de classe
`Building`, pas de visuel). Le trailer se termine maintenant **sur son visuel** (fin du beat 6, juste
avant le titre) — ce n'est donc plus un simple teaser textuel optionnel, c'est le climax du trailer et un
**blocage de production** tant que le visuel n'existe pas.

Options à trancher avant de pouvoir tourner ce plan final :
- Attendre que la feature soit implémentée in-game (building + rendu réel) — dépend du backlog gameplay,
  hors scope de ce document trailer.
- Ou préparer un visuel dédié au trailer en attendant (concept art / rendu fixe / plan composé à la main
  dans un outil externe), à n'utiliser que pour cette bande-annonce, remplacé plus tard par le vrai rendu
  in-game.
- Tant que ce point n'est pas tranché, le beat 6 ne peut pas être tourné dans sa version finale — voir
  question ouverte ci-dessous.

## Saves nécessaires par beat

Saves renommées selon leur beat (`assets/Trailer/saves/`), câblées dans `TrailerDefinition.json` :

| Beat | Save à utiliser | Caractéristiques attendues |
|---|---|---|
| Ouverture + Explore | *(à créer)* | Île fraîchement générée, peu/pas de villes, espace inexploré visible |
| Expand | `02_Expand.json` ☑ | Espace libre suffisant pour que l'autoplay construise routes/outposts de façon visible en ~4s |
| Prestige (insert 2b) | `02_Expand.json` (réutilisée, `ForcedTab: Prestige`) ☑ | Quelques sommets de prestige déjà acquis et visibles à l'écran — ni vide, ni totalement rempli |
| Exploit | `03_Exploit.json` ☑ | Plusieurs villes productives, entrepôts pas encore pleins (pour voir le remplissage) |
| Recherche (insert 3b) | `03_Exploit.json` (réutilisée, `ForcedTab: Research`) ☑ | Plusieurs technologies déjà débloquées, bien réparties dans l'arbre |
| Exterminate | *(à créer)* | Monstre proche d'une ville ou attaque en cours, déclenchable dans une fenêtre de 4s |
| At Your Own Pace | `05_AtYourOwnPace.json` ☑ | Île avancée, prestige disponible, UI TimeControl à vitesse > x1 |
| Underworld / Abyss Gate | `06_AbyssGate.json` ☑ | Abyss Gate construit (`Built: true`) sur la couche Inframonde (hex Q=3, R=-2) ; rendu portail confirmé en place |

**Toutes les saves sont disponibles.** Le trailer complet (30s) est générable : Explore → Expand →
Prestige insert → Exploit → Research insert → Exterminate → At Your Own Pace → Abyss Gate → Title Card.
Le carton titre (beat 7) est rendu par le générateur via `IsTitleCard: true` (fond noir + titre doré centré,
`TitleCardRenderer.Draw`) — pas de save nécessaire, pas de post-prod manuelle.

## Questions ouvertes

- [ ] Ajuster la caméra du beat Exterminate (`04_Exterminate.json`) : actuellement `FitMap`, à passer en
  `Fixed` une fois qu'on a repéré où se déroule l'affrontement avec la civilisation ennemie dans la save.
- [ ] Retravailler le beat Explore si un travelling caméra est souhaité (mode `Keyframes` avec deux points
  de passage) — actuellement `FitMap` (plan large fixe).
- [ ] Desktop uniquement pour ce premier trailer (mobile exclu).

## Journal des itérations

- 2026-06-23 : Premier jet, à partir de l'idée initiale (Explore/Expand/Exploit/Exterminate → At Your
  Own Pace). Saves actuelles identifiées comme placeholders à remplacer.
- 2026-06-23 (révision 1) : Suppression du logo d'ouverture (gameplay visible dès la frame 1, fondu
  rapide ; le nom du jeu n'apparaît plus qu'à la toute fin). Ajout des inserts arbre de prestige (fin
  de section Expand) et arbre de recherche (fin de section Exploit). Ajout d'un beat Inframonde +
  teaser Abyss Gate (feature pas encore implémentée dans le jeu, à traiter en texte seul pour l'instant).
- 2026-06-23 (révision 2) : "At Your Own Pace" déplacé avant le beat Inframonde/Abyss Gate. Le trailer
  se termine maintenant sur le visuel de l'Abyss Gate avec la question "Passerez-vous le portail des
  abysses ?", puis cut direct vers le titre (nouveau beat 7, séparé de l'argument idle). L'absence de
  visuel Abyss Gate devient une dépendance bloquante pour le plan final, plus un simple teaser optionnel.
- 2026-06-23 (révision 3) : Visuel Abyss Gate confirmé en place côté jeu (rendu portail + save de test
  `06_AbyssGate.json` avec la Faille construite sur l'Inframonde). Saves de test renommées par beat
  (`02_Expand.json`, `03_Exploit.json`, `05_AtYourOwnPace.json`, `06_AbyssGate.json`) et câblées dans
  `TrailerDefinition.json` (Expand → Abyss Gate, 19s sur les 30s visés). Restent à fournir : la save
  Explore (ouverture) et la save Exterminate ; le carton logo (beat 7) n'a pas de save et reste hors
  scope du générateur.
- 2026-06-23 (révision 4) : Saves `01_Explore.json` et `04_Exterminate.json` fournies. Carton titre
  (beat 7) implémenté dans le générateur via `IsTitleCard: true` (fond noir + titre doré, `TitleCardRenderer`
  extrait de `GameScreen`) — plus de dépendance post-prod. Pas de musique, tout en anglais. Texte
  AbyssGate traduit ("Will you pass through the Abyss Gate?"). Trailer 30s complet et générable en une passe.
