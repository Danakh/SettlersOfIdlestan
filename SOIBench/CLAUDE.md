# CLAUDE.md — SOIBench

Guidance for Claude Code when working in this subdirectory.

## Ce que fait ce projet

`SOIBench` est un banc de mesure pour la **simulation** (couche modèle/contrôleurs) sur des îles de
fin de partie. Il fait deux choses :

1. **`EndGameStateFactory`** — génère un état de fin de partie synthétique, paramétré par le nombre
   de villes. C'est la pièce maîtresse : atteindre cet état en jouant (`SOIStrategyTester --endless`)
   prend des heures et ne permet pas de faire varier N, alors que N est précisément la variable dont
   on veut mesurer l'effet.
2. **`TickBenchmark` + `ClockProfiler`** — mesurent le coût d'un événement `GameClock.Advanced` et
   l'attribuent à chacun des ~15 contrôleurs abonnés à l'horloge.
3. **`AllocationSampler`** (`--alloc-types`) — dit *quels types* sont alloués, par contrôleur.
4. **`SaveJumpBenchmark`** (`--load-save`) — rejoue un **saut de temps** (celui de `TimeJumpService`)
   sur une **vraie sauvegarde** et attribue le temps à chaque contrôleur, plus le détail des neuf
   étapes de `HarvestController` (`HarvestStepProfiler`). C'est le seul mode qui mesure ce que le
   joueur attend réellement, barre de progression à l'écran. Il affiche aussi ce que le saut a
   **produit** (routes, bâtiments, villes, récoltes) : sans ce dénominateur, un contrôleur en tête
   de la répartition ne dit pas s'il est lent ou simplement très sollicité.
5. **`RenderQueryBenchmark`** (`--render-queries`) — mesure le travail **modèle** d'une image du
   plateau : les agrégats par hexagone que `GameBoardRenderer.Render` reconstruit, puis les requêtes
   que `DrawHarvestIndicator` pose pour chaque tuile. Aucune de ces requêtes ne touche au canvas,
   donc elles se mesurent depuis `SettlersOfIdlestanCore` seul, sans fenêtre ni GPU.

Il ne dépend que de `SettlersOfIdlestanCore`. `SOITests/PerformanceTests/` dépend de lui (et pas
l'inverse) pour valider le générateur.

```bash
dotnet build SOIBench/SOIBench.csproj
dotnet run --project SOIBench -c Release -- --help
```

⚠️ **Toujours mesurer en `-c Release`.** En Debug les mesures sont 3 à 5× plus lentes et les
proportions entre contrôleurs ne sont pas conservées.

## Utilisation typique

```bash
# Table de montée en charge : est-ce linéaire en nombre de villes ?
dotnet run --project SOIBench -c Release -- --cities 50,100,200,400 --csv run.csv

# Un seul point, répartition détaillée, sauvegarde chargeable dans le jeu
dotnet run --project SOIBench -c Release -- --cities 400 --breakdown-top 20 --save-fixture fixtures/

# Coût d'une frame en jeu plutôt que du rattrapage hors-ligne
dotnet run --project SOIBench -c Release -- --cities 400 --ticks-per-event 2

# Chasse aux allocations : quels types, dans quel contrôleur
dotnet run --project SOIBench -c Release -- --cities 400 --alloc-types --breakdown-top 20

# Coût d'une image du plateau, côté modèle (pas la simulation)
dotnet run --project SOIBench -c Release -- --cities 240 --player-share 0.85 --render-queries

# Saut de temps d'une heure rejoué sur une vraie sauvegarde (le cas du joueur qui revient)
dotnet run --project SOIBench -c Release -- --load-save chemin/vers/autosave.json --rounds 3
```

⚠️ **Pour un saut de temps, mesurer sur une sauvegarde, jamais sur la fixture synthétique.** Les
deux postes qui dominaient un saut d'une heure — les prédicats de tâches encore en attente qui
balaient les features, et la propagation Corruption/Dominion — sont invisibles sur la fixture, qui
ne pose que ~15 features et marque toutes les tâches complétées. Mesurée sur la fixture, la même
sauvegarde paraissait saine ; mesurée telle quelle, elle demandait 17 s de CPU pour une heure de
jeu.

⚠️ **`--player-share 0.5` (le défaut) ne ressemble pas à une vraie fin de partie.** Dans une
sauvegarde réelle le joueur possède ~85 % des villes ; à 50 % on mesure surtout l'IA des PNJ, qui
domine alors le rattrapage hors-ligne et fait paraître `NpcGameController` bien plus lourd qu'il ne
l'est en jeu. Pour un profil réaliste, `--cities 240 --player-share 0.85`.

Voir `--help` pour la liste complète des options.

## Comment lire les résultats

| Métrique | Ce qu'elle veut dire |
|---|---|
| `ms / événement` | Médiane sur `--rounds` manches. En jeu, `GameClock.Advance` lève **un seul** événement par frame quelle que soit la vitesse (x1→x10) : c'est donc directement le budget frame consommé par la simulation. Au-delà de ~5 ms il ne reste plus rien pour le rendu. |
| `µs / événement / ville` | Le coût unitaire. C'est lui qu'on cherche à faire baisser ; le total suivra. |
| `allocations / événement` | Pression GC. Compte double en WebAssembly et sur mobile, où une collecte coûte bien plus cher que sur desktop. |
| `rattrapage 8 h hors-ligne` | `AdvanceFromBank` découpe la banque en tranches de 100 ticks, soit un événement par tranche, d'affilée : 8 h d'absence = 288 000 événements. C'est le pire cas réel, et souvent le plus douloureux. |
| `total` (mode `--load-save`) | Secondes de CPU du saut entier. C'est le chiffre que le joueur voit passer sous la barre de progression — le seul qui compte dans ce mode. Le découpage y est celui de `TimeJumpService` (10 000 ticks), pas celui du rattrapage : une heure ne fait que 36 événements, chacun très lourd. Les coupables n'y sont donc pas les mêmes qu'à 100 ticks par événement. |
| `pente` (montée en charge) | Exposant local entre deux tailles consécutives. ≈1 = linéaire (on optimise les constantes), >1,3 = un chemin chaud parcourt plus que ses propres villes — chercher un produit cartésien avant toute micro-optimisation. |

La pente log-log globale affichée en fin de table est biaisée vers le bas quand le coût fixe (IA des
PNJ, indépendante du nombre de villes du joueur) domine les petites tailles. **Lire la pente locale
entre les deux plus grandes tailles**, pas la globale.

## Le générateur — ce qui est fidèle et ce qui ne l'est pas

**Fidèle.** Villes, routes et bâtiments passent par les vrais contrôleurs (`CreateCityFree`,
`BuildBuilding`) : distances minimales entre villes, occupation des vertex, prérequis et niveaux max,
caches, événements. Le résultat est un `MainGameState` sérialisable, chargeable dans le jeu — c'est
ce qui permettra de rejouer le même cas de charge côté rendu. Les tâches du tutoriel sont marquées
complétées (`CompleteAllTutorialTasks`), comme elles le sont en vraie fin de partie : les laisser en
attente faisait réévaluer leurs prédicats à chaque récolte et à chaque vente — plusieurs centaines de
fois par événement d'horloge — et gonflait la mesure de 27 % dans le régime « frame en jeu ». Le
drapeau doit rester posé **avant** `RestartIsland()`, c'est la réinitialisation des contrôleurs qui
reconstruit la liste des tâches en attente.

**Pas fidèle.** La carte est agrandie d'un bloc au lieu de croître hexagone par hexagone via
`AutoExtendController` ; les ressources du joueur sont remises au plafond entre deux constructions ;
l'Inframonde n'appartient qu'au joueur (aucune civilisation agressive n'y est semée) ; les features
(monstres, trésors) restent celles de l'île générée et ne montent donc pas avec la taille de carte.

**Conséquence pratique :** avant de conclure qu'un chemin chaud mesuré ici compte vraiment en jeu,
le recouper avec un vrai run (`SOIStrategyTester --endless`). Le banc dit *combien* ça coûte, pas
*si* le jeu passe réellement par là.

## Contraintes à connaître

- **Le nombre de villes plafonne avec la surface de carte.** La distance minimale entre deux villes
  d'une même civilisation est 3 (2 entre civilisations différentes), soit ~19 vertex bloqués par
  ville. `--surface-radius 16 --underworld-radius 14` (les défauts) portent ~1 800 hexes et
  saturent vers 450–500 villes. Demander plus ne lève pas d'erreur : `EndGameFixture.CityCount`
  contient le compte réellement atteint, et c'est celui qu'affiche la table.
- **`--player-share` n'est pas une garantie.** Elle pondère le Voronoï des territoires puis l'ordre
  de pose, mais plafonne dès que le territoire du joueur sature. Elle reste monotone (plus de part
  demandée → plus de villes joueur), ce que vérifie `Build_HonoursThePlayerShare`.
- **Le premier état mesuré d'un processus paie le JIT de tous les suivants.** `TickBenchmark.WarmUpProcess()`
  est appelé avant la boucle pour cette raison — sans lui la table de montée en charge sort non
  monotone (le point le plus petit apparaît plus lent que le suivant) et devient illisible.
- **`GameClock.SimulateAdvance` avale les exceptions de ses abonnés** (`try { … } catch { }`). Un
  état mal construit « simule » donc sans erreur en ne faisant rien. Ne jamais conclure d'une
  absence d'exception : `Build_ProducesAStateWhereSimulationActuallyRuns` vérifie un effet
  observable (production de ressources) à la place.

## Chasser une allocation

`--alloc-types` s'abonne à `GCAllocationTick` du runtime : un relevé tous les ~100 Ko alloués, avec le
type de l'objet qui franchit le seuil. Les proportions par type sont fiables, les octets exacts non :
c'est un **classement**, pas une comptabilité.

⚠️ **Le classement dit *quoi*, pas *où*.** Les événements GC viennent du runtime natif et arrivent par
EventPipe de façon asynchrone : croiser un échantillon avec le contrôleur en cours d'exécution donne
une répartition crédible mais fausse (en pratique un tirage pondéré par le temps passé dans chaque
contrôleur). Pour le *où*, utiliser la colonne `alloc/évt` de la répartition par contrôleur, qui est
mesurée de façon synchrone sur le même thread — voir `AllocationSampler` pour le détail.

Méthode qui a effectivement fonctionné, et à refaire dans cet ordre :

1. **Lire le classement des types, pas le code.** La première tentative d'optimisation de ce projet a
   été faite « à l'œil » sur ce qui *semblait* coûteux (les chaînes LINQ `Concat` de
   `Civilization.MilitaryVertices`) : gain mesuré 9,1 → 8,9 Mo, soit rien. Les vrais coupables
   n'étaient dans aucune des hypothèses.
2. **Croiser type et contrôleur, chacun depuis sa source.** Un type qui pèse lourd au classement
   alors que plusieurs contrôleurs allouent beaucoup désigne souvent un utilitaire partagé plutôt
   qu'un contrôleur : `HashSet<HexCoord>` en tête pointait vers `VisibleIslandMap`, appelé par
   Harvest, Npc et Military.
3. **Se méfier des types « invisibles ».** `Int32[]` et `Entry[…][]` sont les tableaux internes des
   `HashSet`/`Dictionary` : les additionner à l'entrée de la collection correspondante pour juger de
   son poids réel. Un `Enumerator[T]` signale un `foreach` sur une interface (`IReadOnlyList<T>`,
   `IEnumerable<T>`) qui boxe l'énumérateur de structure — retyper en collection concrète.
4. **Une classe de fermeture (`<>c__DisplayClass…`) ou un `Func<…>` en haut du classement** signale un
   lambda capturant dans une boucle chaude — typiquement un `.FirstOrDefault(c => …)`, à remplacer
   par une boucle indexée ou un accès indexé.
5. **Remesurer après chaque changement.** Plusieurs corrections parfaitement plausibles n'ont rien
   donné.
6. **Connaître le bruit avant d'interpréter un écart.** Sur cette machine, deux exécutions identiques
   du même binaire donnent des médianes qui s'écartent de ~5 % : en dessous, un écart ne veut rien
   dire. Les allocations par événement, elles, sont stables à ~1 % près — c'est le signal à suivre
   quand le temps hésite. En cas de doute, relancer la même mesure trois fois.
7. **Mesurer les deux régimes.** `--ticks-per-event 100` (rattrapage) et `--ticks-per-event 2`
   (frame en jeu) n'ont pas les mêmes coupables : dans le premier, chaque civ PNJ joue un tour d'IA
   par événement et `NpcGameController` domine ; dans le second l'IA ne pèse presque rien et ce sont
   les balayages « toutes civs × toutes villes » de `HarvestController` et `MilitaryController` qui
   font le budget. Une optimisation jugée sur le seul rattrapage peut ne rien donner sur la frame,
   et réciproquement.

## Descendre sous le niveau du contrôleur sans profileur

`dotnet-trace` n'est pas toujours installé, et la répartition intégrée s'arrête au contrôleur. La
méthode qui a marché : **poser des chronomètres temporaires** (un dictionnaire statique `name →
(ticks, appels)` dans le contrôleur suspect, vidé et affiché par le banc), mesurer, puis les
retirer. Deux passages en valent la peine :

- **Le nombre d'appels compte autant que le temps.** `ComputeBuildableRoadsForLayer` sortait à
  722 ms pour **26 appels** : 28 ms l'unité, donc un problème d'algorithme dans la méthode, pas de
  fréquence. À l'inverse `RecalculateForLayer` faisait 162 appels pour 63 ms — beaucoup d'appels,
  chacun bon marché : rien à y gagner.
- **Vérifier une hypothèse en la neutralisant avant de l'optimiser.** La visibilité recalculée à
  chaque route de l'Inframonde *paraissait* être le coupable évident. La remplacer par un `if (false)`
  le temps d'une mesure a montré qu'elle ne pesait que des allocations, pas du temps — et a évité
  une réécriture délicate (cette visibilité est lue comme instantané « avant » par le spawn de PNJ)
  pour rien.

Un corollaire vérifié deux fois : **les allocations et le temps ne bougent pas ensemble.** Une passe
a fait tomber les allocations de 618 à 383 Mo sans changer le temps de plus de 8 %. Sur desktop, le
GC absorbe très bien ce genre de volume ; les allocations restent un objectif en soi (WebAssembly,
mobile), mais ne pas s'attendre à ce qu'elles paient en millisecondes ici.

## Descendre sous le niveau du contrôleur : dotnet-trace

L'échantillonneur intégré donne les types, pas les sites d'appel. Pour ces derniers :

```bash
dotnet-trace collect --format speedscope -o alloc.nettrace --profile gc-verbose \
  -- SOIBench/bin/Release/net10.0/SOIBench.exe --cities 400 --events 700 --rounds 1 --warmup 60
```

Le profil converti est de type **evented** (ouverture/fermeture de frames), pas `sampled` : un
lecteur qui attend des `samples`/`weights` renvoie zéro. Reconstruire la pile au fil des événements
et répartir le poids de chaque intervalle donne le poids inclusif par frame, ce qui suffit à
désigner la méthode fautive. Le poids exclusif, lui, est inutilisable ici (tout retombe sur une
pseudo-frame `CPU_TIME`).

⚠️ La phase de génération de l'état (`EndGameStateFactory`) est tracée elle aussi et pèse ~15 % :
ses frames (`FillBuildings`, `PlaceCities`, et le `BuildBuilding` qu'elles appellent) ne sont pas des
coûts de simulation. Vérifier qu'une frame suspecte est bien sous `GameClock.SimulateAdvance` avant
d'y toucher — c'est ce qui a évité d'« optimiser » `TaskRecordController.HandleBuildingBuilt`, qui
paraissait peser 7 % mais n'était appelé que par la génération.

## ClockProfiler — le point fragile

`ClockProfiler` atteint les abonnés de `GameClock.Advanced` **par réflexion sur le champ de
sauvegarde de l'événement** (un événement « field-like » en C# a un champ privé du même nom), puis
remplace la liste d'invocation par la même liste, dans le même ordre, chaque délégué enveloppé dans
un chronomètre. L'ordre est préservé volontairement : il est significatif (le combat doit être résolu
avant le déplacement des monstres — voir `MainGameController.InitializeControllersForCurrentIsland`).

C'est le prix à payer pour ne pas ajouter de crochet de profilage dans le code du jeu. Si
`GameClock.Advanced` est renommé, ou réimplémenté avec des accesseurs `add`/`remove` explicites, la
réflexion échoue silencieusement et la répartition par contrôleur devient vide.
`ClockProfiler_AttachesAndAttributesTimeToControllers` (dans `SOITests`) est le garde-fou : il
échoue explicitement dans ce cas.

`HarvestStepProfiler` a la même fragilité pour le champ privé `HarvestController._steps` : renommé
ou remplacé par autre chose qu'un tableau de `ProductionStep(string, Action<long>)`, la réflexion
échoue et le détail par étape disparaît (`IsAttached` faux), sans que le reste de la mesure en
souffre. Ce détail est indispensable ici : `HarvestController` pesait 46 % d'un saut d'une heure, et
c'est la Fonderie — pas la récolte automatique qu'on soupçonnait — qui en portait l'essentiel.

## Ordre de travail sur les perfs

1. **Mesurer d'abord la montée en charge**, jamais un point isolé — c'est la pente qui dit s'il faut
   chercher un algorithme ou des constantes.
2. **Profiler ensuite** (échantillonnage : dotnet-trace, Visual Studio) pour descendre sous le niveau
   du contrôleur. La répartition de SOIBench dit *quel* contrôleur, pas *quelle ligne*.
3. **Optimiser avec le filet en place** : les 1 294 tests de `SOITests`, les scénarios FullIsland, et
   surtout le déterminisme du PRNG — les sauvegardes de `SOITests/saves/current/` doivent se
   régénérer à l'identique. Une optimisation qui change une seule valeur tirée du PRNG casse tous les
   scénarios, et c'est voulu.
4. **Le rendu vient après**, avec la même fixture chargée dans le head Desktop via `--save-fixture`.
   Tant que la simulation mange le budget frame, mesurer le rendu ne dit rien d'utile.

## Le rendu : ce que `--render-queries` mesure, et ce qu'il ne mesure pas

Il mesure les **requêtes modèle** d'une image, pas les appels de dessin. C'est délibéré : ce sont
elles qui dégénéraient avec la taille de la partie, pas le dessin. Le dessin est proportionnel au
nombre de tuiles visibles ; les requêtes l'étaient au **produit** tuiles × villes du joueur, parce
que `GetAutoHarvestInfoForHex` et `GetManualHarvestableResources` répondaient à « quelles villes
bordent cet hexagone ? » par un balayage de toutes les villes de la civilisation. À 1 027 tuiles et
200 villes joueur, cela faisait ~200 000 tests d'adjacence et ~700 Ko de déchets **par image**, avant
le moindre pixel. `Civilization.GetCitiesAdjacentTo` (index hexagone → villes) a ramené la question à
une recherche dans un dictionnaire.

Trois pièges quand on remesure :

- **La carte visible de la fixture n'est pas celle d'une vraie fin de partie.** Le générateur ne pose
  ni exploration ni Œil de Dieu, et `Visibility` n'y découvre que ~80 hexagones sur 1 027. Mesurer
  dessus sous-estime le coût d'un facteur 13 et donne un résultat rassurant et faux. Le banc utilise
  donc la carte complète (`GetMapForZ`), qui est ce que le joueur voit en fin de partie.
- **Le générateur ne pose que ~15 features**, là où une sauvegarde réelle en compte plus de 2 000
  (Dominion et Corruption s'accumulent hexagone par hexagone). Tout ce qui parcourt `Features` est
  donc massivement sous-estimé ici — c'est le poste « agrégats de features », à relire sur une vraie
  sauvegarde avant de le déclarer négligeable.
- **`FeatureAggregates` est une réplique** de ce que fait `GameBoardRenderer` : SOIBench ne dépend pas
  du projet Skia. Les deux doivent être tenus d'accord à la main.
