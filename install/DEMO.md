# Démo Steam

La démo est **le même code que le jeu complet**, compilé avec la constante `DEMO`. Elle est
publiée sous un App ID Steam distinct, avec ses propres dépôts.

## D'où vient le mode démo

`GameSettings.DemoMode` est mis à `true` par `MainWindow` quand le head Desktop a été compilé
avec la constante `DEMO` (`#if DEMO`). Cette constante vient de la propriété MSBuild
`DemoBuild`, à `false` dans le dépôt : ce sont les scripts `build_desktop_demo_*.bat` qui
passent `-p:DemoBuild=true`.

Conséquence : **le mode démo dépend du script lancé, jamais de la branche checkoutée.** Une
branche `demo` reste utile pour porter du contenu propre à la démo, mais elle n'est pas
nécessaire pour que le binaire soit une démo, et n'est pas suffisante non plus — c'est
`build_desktop_demo_*.bat` qui décide.

Hors build démo, le drapeau reste lisible en ligne de commande (`--demo`) pour tester
localement. Dans une build démo il est câblé en dur : un joueur ne peut pas lever les
restrictions en retirant l'argument de son raccourci.

Ce que la démo restreint (voir `SOITests/ControllerTests/DemoModeTests.cs`) : sommets de
prestige coûtant plus de 100 points, recherches de palier 4 et au-delà, plafond de points de
prestige, et fin de démo après la 3ᵉ île. L'écran-titre affiche `changelog_demo_fr/en.txt` au
lieu du changelog habituel.

## Fichiers

| Fichier | Versionné | Rôle |
|---|---|---|
| `install/build_desktop_demo_win.bat` | oui | publie en `-p:DemoBuild=true` vers `steamcontent/demo_win64` |
| `install/build_desktop_demo_linux.bat` | oui | idem vers `steamcontent/demo_linux64` |
| `steamcontent/scripts/upload_demo.bat` | oui | vérifie le contenu puis lance steamcmd |
| `steamcontent/scripts/build_and_upload_demo.bat` | oui | enchaîne build + upload, Windows puis Linux |
| `steamcontent/scripts/app_build_demo_template.vdf` | oui | gabarit, `TODO` à la place des identifiants |
| `steamcontent/scripts/depot_build_demo_template.vdf` | oui | gabarit, `TODO` à la place des identifiants |
| `steamcontent/scripts/app_build_demo_{win,linux}.vdf` | **non** | portent l'App ID de la démo |
| `steamcontent/scripts/depot_build_demo_{win,linux}.vdf` | **non** | portent les Depot ID de la démo |
| `steamcontent/scripts/deploy_demo.bat` | **non** | `build_and_upload_demo.bat` avec le login Steam |
| `steamcontent/demo_win64`, `demo_linux64` | **non** | contenu généré |

Même partage que pour le jeu complet : les gabarits sont dans git, les fichiers portant les
identifiants et le login n'y sont pas. Sur une machine neuve, recopier les gabarits et y
remplacer les `TODO` par l'App ID et les Depot ID de la démo, relevés dans Steamworks.

## Garde-fous

Les contenus démo et jeu complet vivent dans des répertoires séparés (`demo_win64` vs `win64`),
et les scripts de build démo repartent d'un `obj` vide pour que `MainWindow` soit bien
recompilé avec la constante.

En plus de ça, `build_desktop_demo_*.bat` dépose un fichier `demo_build.marker` dans le
répertoire de contenu. `upload_demo.bat` **exige** ce marqueur — il refuse d'envoyer un
répertoire qui n'a pas été produit par le script démo. `upload.bat` (jeu complet) le **refuse** —
il s'arrête si un contenu démo a atterri dans `win64` / `linux64`. Le marqueur est exclu du
dépôt Steam par les `depot_build_demo_*.vdf`, il n'arrive donc pas chez le joueur.

Vérification indépendante d'une build : la chaîne `--demo` est présente dans le
`SettlersOfIdlestan.dll` du jeu complet (le drapeau y est lu sur la ligne de commande) et
absente de celui de la démo (il y est câblé en dur).

## Procédure

```bat
rem tout en un, depuis install\steamcontent\scripts
deploy_demo.bat
```

ou étape par étape :

```bat
install\build_desktop_demo_win.bat
install\steamcontent\scripts\upload_demo.bat <login> win
install\build_desktop_demo_linux.bat
install\steamcontent\scripts\upload_demo.bat <login> linux
```

`SetLive` est vide dans les `app_build_demo_*.vdf` : le build monte sur Steamworks mais n'est
pas public. Le passer en live à la main depuis la page Builds de l'application démo.
