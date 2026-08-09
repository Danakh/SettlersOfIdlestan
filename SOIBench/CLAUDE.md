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
```

Voir `--help` pour la liste complète des options.

## Comment lire les résultats

| Métrique | Ce qu'elle veut dire |
|---|---|
| `ms / événement` | Médiane sur `--rounds` manches. En jeu, `GameClock.Advance` lève **un seul** événement par frame quelle que soit la vitesse (x1→x10) : c'est donc directement le budget frame consommé par la simulation. Au-delà de ~5 ms il ne reste plus rien pour le rendu. |
| `µs / événement / ville` | Le coût unitaire. C'est lui qu'on cherche à faire baisser ; le total suivra. |
| `allocations / événement` | Pression GC. Compte double en WebAssembly et sur mobile, où une collecte coûte bien plus cher que sur desktop. |
| `rattrapage 8 h hors-ligne` | `AdvanceFromBank` découpe la banque en tranches de 100 ticks, soit un événement par tranche, d'affilée : 8 h d'absence = 288 000 événements. C'est le pire cas réel, et souvent le plus douloureux. |
| `pente` (montée en charge) | Exposant local entre deux tailles consécutives. ≈1 = linéaire (on optimise les constantes), >1,3 = un chemin chaud parcourt plus que ses propres villes — chercher un produit cartésien avant toute micro-optimisation. |

La pente log-log globale affichée en fin de table est biaisée vers le bas quand le coût fixe (IA des
PNJ, indépendante du nombre de villes du joueur) domine les petites tailles. **Lire la pente locale
entre les deux plus grandes tailles**, pas la globale.

## Le générateur — ce qui est fidèle et ce qui ne l'est pas

**Fidèle.** Villes, routes et bâtiments passent par les vrais contrôleurs (`CreateCityFree`,
`BuildBuilding`) : distances minimales entre villes, occupation des vertex, prérequis et niveaux max,
caches, événements. Le résultat est un `MainGameState` sérialisable, chargeable dans le jeu — c'est
ce qui permettra de rejouer le même cas de charge côté rendu.

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
   du même binaire donnent des médianes allant de 5,58 à 6,15 ms : un écart inférieur à ~5 % ne veut
   rien dire. Les allocations par événement, elles, sont stables à ~1 % près — c'est le signal à
   suivre quand le temps hésite. En cas de doute, relancer la même mesure trois fois.

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
