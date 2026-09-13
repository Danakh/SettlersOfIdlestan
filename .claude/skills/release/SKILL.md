---
name: release
description: Sortie d'une nouvelle version du jeu — build, tests, revue du changelog validée par l'utilisateur, création de la branche release/vX.Y, bump de version, régénération des saves de test, push de release et main. À invoquer quand l'utilisateur demande de sortir, publier ou livrer une nouvelle version.
---

# Sortie d'une nouvelle version

Procédure ordonnée. Les étapes dépendent les unes des autres : ne jamais en sauter une, ne
jamais en réordonner.

## Règles valables pour toute la procédure

- **Arrêt net au premier échec.** Build rouge, test rouge, working tree sale, commande en
  erreur : on s'arrête, on rapporte ce qui a échoué avec la sortie, et on rend la main. Ne
  jamais corriger le problème et poursuivre la release : c'est à l'utilisateur de décider s'il
  corrige ou s'il reporte la sortie.
- **Commits autorisés.** Ce skill déroge explicitement à la règle « ne jamais commiter » de
  CLAUDE.md, mais **uniquement** pour les deux commits décrits ici (revue du changelog,
  ouverture de la version suivante). Aucun autre commit, aucun amend, aucun rebase.
- **Ne pas toucher au bandeau d'en-tête du changelog** (les lignes situées au-dessus du premier
  `---`, avis bêta / démo). L'utilisateur le maintient lui-même.
- Les fichiers du projet contiennent des accents : les modifier avec Edit, jamais avec Write.

## 0. Pré-vol

1. `git status --porcelain` : le working tree doit être **vide**. Sinon, arrêt : lister ce qui
   traîne et demander à l'utilisateur de commiter ou de stasher.
2. `git rev-parse --abbrev-ref HEAD` : doit être `main`. Sinon, arrêt.
3. `git fetch origin` puis `git status -sb` : main doit être à jour avec `origin/main` (ni en
   retard, ni divergente). En avance de quelques commits locaux : normal, on continue.
4. Lire `SettlersOfIdlestan/Model/Game/GameVersion.cs` pour récupérer `X.Y.Z`.
   - **Version sortie** = `vX.Y` → branche `release/vX.Y`.
   - **Version suivante** = `X.(Y+1).0` (le patch repart toujours à 0).
   - Vérifier que `release/vX.Y` n'existe pas déjà (`git branch -a --list 'release/*'`), ni en
     local ni sur origin. Si elle existe, arrêt : la version a déjà été sortie.
5. Vérifier que le dernier bloc des changelogs FR et EN est bien `vX.Y`. Désynchronisation entre
   `GameVersion.Current` et le changelog : arrêt, c'est à l'utilisateur de trancher.

Annoncer à l'utilisateur : version sortie, version suivante, branche qui sera créée.

## 1. Build

```
dotnet build SettlersOfIdlestan.slnx
```

Échec → arrêt.

## 2. Tests

Les deux projets, dans cet ordre :

```
dotnet test SOITests
dotnet test SOIUITests
```

Longs : prévoir un timeout généreux (10 min) ou un lancement en arrière-plan. Le moindre test
rouge → arrêt, en donnant le nom des tests en échec.

### Le race gauntlet

Désactivé dans les manches ordinaires (`ManualTheory` sur `SOI_RACE_GAUNTLET`), mais **obligatoire
pour une release** : c'est le seul test qui vérifie que chaque race implémentée reste jouable de
bout en bout — partie de l'île où l'Ascension la dépose jusqu'à la fin de l'île 6, prestiges
enchaînés, seed 1.

```
SOI_RACE_GAUNTLET=1 dotnet test SOITests --filter "FullyQualifiedName~RaceGauntletTests"
```

(En PowerShell : `$env:SOI_RACE_GAUNTLET=1; dotnet test ...`.) **Très long** — un cas par race, de
quelques minutes à quelques dizaines de minutes chacun : lancer en arrière-plan ou avec le timeout
maximum, et prévenir l'utilisateur que cette étape domine la durée de la release.

Toutes les races doivent passer. Un cas rouge → arrêt, en nommant la race et en reprenant la ligne
de verdict de la sortie console ; les CSV et la sauvegarde de l'état final sont sous
`%TEMP%\soi-race-gauntlet-tests\<Race>\`, chargeables dans le Desktop pour diagnostiquer. Ne jamais
requalifier un échec en simple « race faible » ni en malchance de seed pour poursuivre : c'est à
l'utilisateur de trancher entre corriger et reporter la sortie.

## 3. Revue du changelog (validation humaine obligatoire)

Réunir la matière et la présenter à l'utilisateur :

1. Point de départ = branche `release/vX.(Y-1)` si elle existe, sinon la dernière branche
   `release/v*` par tri numérique (attention : v0.9 < v0.10).
2. `git log <branche précédente>..main --oneline` : la liste complète des commits de la version.
3. Le bloc `vX.Y` actuel de `changelog_fr.txt` et son équivalent EN.

Puis proposer, sans rien écrire encore :
- les entrées manquantes (une ligne par fonctionnalité, dans le style des blocs existants),
- les entrées à corriger ou à supprimer,
- toute désynchronisation FR / EN (même nombre d'entrées, même ordre, même sens).

Filtre de contenu (règle de CLAUDE.md) : **seules** les nouveautés de gameplay significatives
(nouveaux systèmes, nouvelles mécaniques, nouveau contenu). Pas d'équilibrage, pas de correction
de bug, pas de refactor, pas de polish UI.

**Attendre la validation explicite de l'utilisateur.** S'il demande des modifications, les
appliquer (Edit, FR **et** EN) et redemander validation.

Si le changelog a été modifié, commiter uniquement les deux fichiers de changelog :

```
git add SettlersOfIdlestanSkia/Resources/changelog/changelog_fr.txt SettlersOfIdlestanSkia/Resources/changelog/changelog_en.txt
git commit -m "changelog vX.Y"
```

Si rien n'a changé, pas de commit.

## 4. Branche de release

La branche part du commit courant de main, **avant** le bump : elle doit porter la version
qu'elle publie.

```
git branch release/vX.Y
```

Rester sur main, ne pas basculer dessus.

## 5. Bump de version et changelog vide

Sur main, avec Edit :

1. `SettlersOfIdlestan/Model/Game/GameVersion.cs` : `Current = "X.(Y+1).0"`.
2. `changelog_fr.txt` et `changelog_en.txt` : insérer le nouveau bloc en tête, juste après le
   bandeau, en respectant exactement la mise en forme existante :

```
vX.(Y+1)

---

```

Bloc vide, aucune puce : il se remplira au fil de la version.

## 6. Régénération des saves de test

**Indispensable** : `SaveUtils` vérifie que chaque save de `SOITests/saves/current/` porte
`GameVersion.Current`. Depuis le bump, tout `StepIslandCurrentTests` est rouge tant que ce
rebuild n'a pas tourné.

```
SOI_MANUAL_TESTS=1 dotnet test SOITests --filter "FullyQualifiedName~Rebuild_All_Current_Saves"
```

(En PowerShell : `$env:SOI_MANUAL_TESTS=1; dotnet test ...`.) Très long — une trentaine de steps
simulés : lancer en arrière-plan ou avec le timeout maximum.

Puis **revalider** :

```
dotnet test SOITests
```

Rouge à ce stade → arrêt : la release n'est pas saine.

Le rebuild réécrit `SOITests/saves/run_current.csv` (les saves elles-mêmes sont gitignorées).
Montrer son diff à l'utilisateur : c'est le résumé d'équilibrage (prestiges, villes, niveaux de
bâtiments, recherches). Une dérive importante n'est pas bloquante, mais doit être signalée.

## 7. Commit d'ouverture

Vérifier d'abord avec `git status --porcelain` qu'il ne traîne rien d'inattendu.

```
git add SettlersOfIdlestan/Model/Game/GameVersion.cs SettlersOfIdlestanSkia/Resources/changelog/changelog_fr.txt SettlersOfIdlestanSkia/Resources/changelog/changelog_en.txt SOITests/saves/run_current.csv
git commit -m "ouverture vX.(Y+1)"
```

## 8. Push

Uniquement si **aucune** étape précédente n'a échoué. L'utilisateur a autorisé ce push dans le
cadre de ce skill : ne pas redemander, mais l'annoncer.

```
git push origin release/vX.Y
git push origin main
```

## 9. Rapport final

Résumer en quelques lignes : version sortie, branche créée et poussée, nouvelle version
courante, entrées de changelog ajoutées pendant la revue, dérive éventuelle de
`run_current.csv`.

Rappeler que le build et l'upload Steam (`install/steamcontent/scripts/deploy.bat`) restent à la
main de l'utilisateur, depuis la branche `release/vX.Y` — ce skill ne les lance pas.
