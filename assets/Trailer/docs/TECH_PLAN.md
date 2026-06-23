# Plan technique — Générateur de bande-annonce (SOITrailerGenerator)

Document de référence technique, à relire/modifier à chaque itération. Voir
[STORYBOARD.md](STORYBOARD.md) pour le découpage créatif que ces fonctionnalités permettent de réaliser.

## État global : les 7 chantiers prévus sont implémentés (2026-06-23)

Tout ce qui suit a été codé et testé bout-en-bout (génération des frames + assemblage ffmpeg automatique
sur les 3 saves de test actuelles). Reste à faire : fournir les vraies saves/visuels listés dans
`STORYBOARD.md` et composer le `TrailerDefinition.json` final du storyboard (le JSON actuel est encore
la version de test à 3 séquences simples, sans texte/keyframes/autoplay).

## Architecture actuelle

- `SOITrailerGenerator/Program.cs` — point d'entrée console. Cherche `assets/Trailer`, génère les frames,
  puis lance ffmpeg automatiquement (cherche le binaire dans le `PATH` puis dans le dossier de fallback
  connu). `--skip-ffmpeg` génère uniquement les frames sans encoder.
- `SOITrailerGenerator/Trailer/TrailerService.cs` — lit `TrailerDefinition.json`, recrée la pile de
  services/renderers pour chaque séquence (`CaptureSequence`), applique le profil mobile/desktop,
  l'onglet forcé, la couche affichée et l'autoplay, assemble la commande ffmpeg finale (avec `-y`).
- `SOITrailerGenerator/Trailer/VideoExportController.cs` — boucle frame par frame : avance la simulation
  (`SimulateAdvance`), appelle `autoplayTick` si fourni, interpole la caméra via `cameraAtTime(t)`, dessine
  la scène puis les `TextCues` par-dessus, encode chaque frame en PNG.
- `SOITrailerGenerator/Trailer/TrailerCameraTrack.cs` — construit la fonction caméra-à-l'instant-t à
  partir d'une `TrailerCamera` (FitMap/Fixed → constante ; Keyframes → interpolation lerp + easing).
- `SOITrailerGenerator/Trailer/TrailerDefinition.cs` — schéma JSON complet (voir plus bas).
- `SOITrailerGenerator/Trailer/CompositeGameRenderer.cs` — combine île + overlay UI + tooltips.
- `SOITrailerGenerator/Trailer/NullFileSystemService.cs` — stub, aucune sauvegarde réelle pendant le rendu.
- `SettlersOfIdlestanSkia/Services/UILayoutService.cs` — `SetForceMobile(bool)` ajouté pour piloter le
  layout HUD depuis le générateur sans passer par le toggle utilisateur.
- `SettlersOfIdlestanSkia/Renderers/Overlay/OverlayRenderer.cs` — `SwitchToResearchTab()` ajouté à côté
  de `SwitchToPrestigeTab()`/`SwitchToIslandTab()` existants.

## Schéma JSON (`TrailerDefinition`, état actuel du code)

```json
{
  "Width": 1920, "Height": 1080, "Fps": 30,
  "Sequences": [
    {
      "Save": "explore_intro.json",
      "DurationSeconds": 6,
      "SimulationSpeedMultiplier": 1.5,
      "DeviceProfile": "Desktop",
      "ForcedTab": "None",
      "ViewedLayer": "Surface",
      "AutoplayProfile": "None",
      "Camera": {
        "Mode": "Keyframes",
        "Keyframes": [
          { "TimeSeconds": 0, "X": 0, "Y": 0, "Zoom": 2.5, "Easing": "EaseOut" },
          { "TimeSeconds": 6, "X": 0, "Y": 0, "Zoom": 1.0, "Easing": "Linear" }
        ]
      },
      "TextCues": [
        { "Text": "EXPLORE", "StartSeconds": 0.3, "EndSeconds": 2.0, "FadeSeconds": 0.3, "Position": "Bottom", "FontSizePx": 64 }
      ]
    }
  ]
}
```

- `Camera.Mode` : `FitMap` (cadre toute l'île, constant), `Fixed` (X/Y/Zoom constants), `Keyframes`
  (interpole `Keyframes`, `Easing` ∈ `Linear`/`EaseIn`/`EaseOut`/`EaseInOut`).
- `DeviceProfile` ∈ `Desktop`/`Mobile`.
- `ForcedTab` ∈ `None`/`Island`/`Prestige`/`Research` — onglet HUD affiché pendant toute la séquence.
- `ViewedLayer` ∈ `Surface`/`Underworld` — la save chargée doit déjà avoir débloqué la couche demandée.
- `AutoplayProfile` ∈ `None`/`Expand`/`Exploit`/`Exterminate` — pilote `CivilizationAutoplayer` une fois
  par frame (`TryStep0Once` / `TryStep1Once`+`TryStep2Once` / `TryMilitaryStepOnce`).

## Détail par chantier

### 1. Texte incrusté — ☑ Fait
`TrailerTextCue` (`TrailerDefinition.cs`) + rendu dans `VideoExportController.DrawTextCues` (fade linéaire
in/out via `FadeAlpha`, police `SkiaFonts.Bold`, dessiné via `SkiaTextUtils.DrawText`).

### 2. Caméra dynamique (keyframes) — ☑ Fait
`TrailerCameraKeyframe`/`TrailerCameraEasing` (`TrailerDefinition.cs`) + interpolation dans
`TrailerCameraTrack.Build/Interpolate`. `VideoExportController.CaptureSequence` prend désormais
`Func<float, (SKPoint Position, float Zoom)> cameraAtTime` au lieu d'une position/zoom fixes.

### 3. Profil mobile/desktop — ☑ Fait
`TrailerDeviceProfile` + `UILayoutService.SetForceMobile(bool)` appelé dans `TrailerService.CaptureSequence`.

### 4. Onglet UI forcé (inserts prestige/recherche) — ☑ Fait
`TrailerForcedTab` + `OverlayRenderer.SwitchToPrestigeTab()`/`SwitchToResearchTab()`/`SwitchToIslandTab()`
appelés juste après `sceneRenderer.Initialize(...)`.

### 5. Bascule de layer (beat Underworld) — ☑ Fait
`TrailerViewedLayer` + assignation directe de `WorldState.CurrentViewedLayer` (`IslandMap.SurfaceLayer`
ou `LayerState.UnderworldZ`) juste après le chargement de la save.

### 6. Autoplay piloté par séquence — ☑ Fait
`TrailerAutoplayProfile` + `TrailerService.BuildAutoplayTick` construit un `CivilizationAutoplayer` câblé
sur les contrôleurs de `gameControllerService.MainGameController` et renvoie l'action appelée une fois
par frame dans `VideoExportController` (après `SimulateAdvance`).
**Point de vigilance non résolu** : le rythme des constructions dépend de l'état de la save (ressources,
prérequis). À ajuster avec les vraies saves cibles (durée de séquence / `SimulationSpeedMultiplier`) une
fois les saves du storyboard disponibles — non testé avec une vraie save d'expansion/combat pour l'instant.

### 7. Génération finale automatique (ffmpeg) — ☑ Fait
`Program.RunFfmpeg` cherche `ffmpeg` (PATH puis fallback `C:\Program Files\ffmpeg-8.1.1-essentials_build\bin`),
lance le process et vérifie le code de sortie. `--skip-ffmpeg` pour ne générer que les frames.
**Bug corrigé en cours de route** : la commande ffmpeg générée n'avait pas `-y` — en exécution non
interactive (stdin non connecté), ffmpeg refusait d'écraser un `trailer.mp4` existant et abandonnait sans
que le code de sortie ne soit toujours fiable pour le détecter. `-y` ajouté dans `TrailerService.cs` et
`VideoExportController.cs`.

## Abyss Gate — état de la dépendance

Le visuel de l'Abyss Gate (cf. STORYBOARD.md, climax du beat 6/final du trailer) est en cours de
préparation côté jeu et sera bientôt disponible. Aucun travail spécifique n'est nécessaire côté
générateur pour l'afficher : une fois la feature/le visuel intégré au jeu, le plan final se filmera
comme n'importe quelle autre scène d'île (save dédiée + caméra + texte incrusté). Pas de chantier
technique séparé à prévoir ici — seulement attendre la save de test correspondante.

## Prochaines étapes

1. Préparer les saves de test listées dans `STORYBOARD.md` (île fraîche pour Explore, save Inframonde,
   save avec monstre pour Exterminate, etc.).
2. Composer le vrai `TrailerDefinition.json` du storyboard (8 beats, textes, keyframes caméra, profils
   autoplay) en remplacement du fichier de test actuel à 3 séquences.
3. Tester le rythme de l'autoplay (`AutoplayProfile`) sur les vraies saves et ajuster les durées.
4. Brancher la save Abyss Gate dès que le visuel est prêt côté jeu.

## Journal des itérations

- 2026-06-23 : État des lieux de l'architecture existante + plan en 5 chantiers, à partir de la demande
  initiale (accélération du temps déjà en place, reste : texte, caméra, mobile, autoplay, export ffmpeg).
- 2026-06-23 (révision) : Ajout de 2 chantiers issus de la révision du storyboard — onglet UI forcé
  (`TabBarRenderer.SetActiveTab`, public) pour les inserts prestige/recherche, et bascule de layer
  (`WorldState.CurrentViewedLayer`, public) pour le beat Underworld. Abyss Gate : le visuel arrive
  prochainement côté jeu, pas de chantier dédié côté générateur — juste une save à fournir une fois prête.
- 2026-06-23 (implémentation) : Les 7 chantiers sont codés et testés bout-en-bout (régénération complète
  des 450 frames de test + assemblage `trailer.mp4` automatique). Bug ffmpeg sans `-y` trouvé et corrigé
  au passage (échec silencieux en exécution non interactive face à un fichier de sortie existant).
