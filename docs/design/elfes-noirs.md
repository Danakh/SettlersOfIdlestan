# Design — Les Elfes noirs

> **Statut** : ✅ **implémentée côté modèle / contrôleurs**, ⬜ **UI non faite** (choix délibéré — voir §13).
> **Race** : `RaceId.DarkElf`, `RaceTier.Advanced` (seconde rangée de pouvoirs divins).
> **Portée** : la race + un nouveau Monument (la Percée de Surface) + 2 nouvelles `ECategory`.

## État d'implémentation

| # | Morceau | État |
|---|---|---|
| §3 | Départ souterrain (`StartsInUnderworld`, triangle paramétrable, vertex d'arrivée mémorisé) | ✅ |
| §4 | Kit de recherches offert (`STARTING_RESEARCH`) | ✅ |
| §5 | Vertex de prestige racial offert | ✅ |
| §6 | Monument Percée de Surface (feature + contrôleur + perte de la surface) | ✅ |
| §6.2bis | Site d'Arrivée — réservation inattaquable du point de chute | ✅ |
| §7 | Pacte des Profondeurs (`MONSTER_ATTACK_IMMUNITY`) | ✅ |
| §8 | Bâtiment racial Sanctuaire de l'Araignée | ✅ |
| §9 | Définition finale de la race — la race est sélectionnable | ✅ |
| — | Autoplay (`CivilizationAutoplayer`) et `SOIStrategyTester` | ✅ |
| — | Localisation `fr.json` / `en.json` | ✅ |
| — | Tests (`DarkElfRaceTests`, `SurfaceBreachControllerTests`) | ✅ |
| §13 | UI Skia (panneau d'investissement, placement, journal, tooltips de modifiers) | ⬜ |

Suite de tests complète au vert : 1294 tests.

---

## 1. Concept

**Peuple des profondeurs.** Les elfes noirs ne descendent pas dans l'Inframonde : ils en viennent.
La surface est pour eux ce que l'Inframonde est pour les autres races — un ailleurs qui se mérite,
au prix d'un Monument. En échange, les monstres des profondeurs les reconnaissent comme des leurs.

La partie s'ouvre donc sur la couche `LayerState.UnderworldZ`, avec un unique avant-poste posé sur
un triangle **Caverne aux champignons / Colline / Montagne**, et l'île de surface générée mais
inaccessible.

**Décision de cadrage : la race n'a aucun malus chiffré pour le moment.** Le départ souterrain est
déjà une contrainte structurelle forte (voir §3.3) ; on équilibre par retrait de bonus plutôt que
par ajout de malus si la race s'avère trop puissante.

---

## 2. État existant — ✅ résolu

Le stub inerte a été remplacé par la définition du §9. `RaceDefinition.IsImplemented =>
RacialBuilding != null` étant satisfait par le Sanctuaire de l'Araignée, la race apparaît désormais
dans `AscensionController.GetSelectableRaces` dès la seconde rangée de pouvoirs divins complète.

> Le test `RaceSystemTests.GetSelectableRaces_SecondRowComplete_AddsImplementedAdvancedRaces`
> encodait explicitement « les Elfes noirs ne sont jamais sélectionnables » : il assertait le stub, il
> asserte maintenant l'inverse.

---

## 3. Le départ souterrain

### 3.1 Comment ça marche aujourd'hui

`IslandMapGenerator.GenerateIsland` pose la ville du joueur en surface via `PopulatePlayerCivilization`
(`Controller/Generator/IslandMapGenerator.cs:543`), sur le vertex garanti `startVertexTerrain`/Forêt/Eau
par `EnsureStartPairNearEdge` (`:558`). Le terrain d'accompagnement vient de la race
(`RaceDefinition.StartVertexTerrain`, Colline par défaut, Montagne pour les Nains).

L'Inframonde, lui, sait déjà se créer seul : `LayerState.EstablishOupostInNewAutoExpandLayer`
(`Model/IslandMap/LayerState.cs:72`) fabrique un triangle de 3 hexes, y pose un avant-poste au vertex
partagé et active `AutoExtend`. Il est aujourd'hui appelé par `DeepestMineController.TryInitializeUnderworld`
(`Controller/Expand/DeepestMineController.cs:77`) et par `AbyssGateController` pour l'Abysse — rien ne
le lie à la Mine Profonde.

### 3.2 Modifications — ✅ fait

1. **`RaceDefinition`** — trois nouvelles propriétés optionnelles plutôt qu'un seul flag :
   `StartsInUnderworld`, `UnderworldStartTerrains` (le trio de terrains, porté par la race plutôt que
   codé en dur dans le générateur) et `FreePrestigeVertices` (§5).
2. **`LayerState.EstablishOupostInNewAutoExpandLayer`** — nouveau paramètre optionnel
   `triangleTerrains`. Null ou incomplet = Montagne partout : comportement strictement inchangé pour
   la Mine Profonde et l'Abysse.
3. **`IslandMapGenerator`** :
   - `GenerateIsland` gagne `populatePlayerCity` (false = carte générée à l'identique, mais aucune
     ville posée) et expose le vertex résolu via la propriété `LastSurfaceStartVertex` ;
   - `GenerateWorldState` reçoit désormais une `RaceDefinition? race` **à la place** de
     `startVertexTerrain` (le terrain de départ en est déduit). Les 3 appelants — `AscensionController`,
     `PrestigeController`, `MainGameController.RestartIsland` — passent maintenant `race:` ;
   - quand la race démarre sous terre : le vertex de surface est mémorisé dans
     `LayerState.ArrivalVertex` de la couche 0, la couche `UnderworldZ` est créée en fin de génération
     avec le triangle de la race, et `CurrentViewedLayer` bascule sur l'Inframonde.

**Ajout non prévu au design initial — l'ancrage de surface.** Le plan d'origine laissait
`PlaceNpcCivilizations` (`if (state.PlayerCivilization.Cities.Count == 0) return false;`) et les trois
placements de features qui lisent `PlayerCivilization.Cities[0]` sans point de référence : l'île
serait née **sans aucun NPC** et avec des features posables sur le futur vertex d'arrivée. C'est
exactement le risque n°2 du §11, et il se déclenchait dès la première partie.

Correctif : `IslandMapGenerator.GetSurfaceAnchorVertex(worldState)` renvoie la ville de surface du
joueur, ou à défaut le `ArrivalVertex` mémorisé. Les trois usages de `Cities[0]` passent par lui, et
`NpcCivilizationPlacer.PlaceNpcCivilizations` gagne un paramètre `playerAnchorVertex`. Les NPC et les
features gardent donc leurs distances de placement habituelles.

### 3.3 Pourquoi ce triangle exactement

Ce n'est pas cosmétique. Le pool de terrains de l'Inframonde
(`Controller/Island/AutoExtendController.cs:26-33`) est :

> Montagne ×10, Désert ×2, Colline ×2, **Caverne aux champignons ×4**, Filon de Mithril ×1, Grotte de Cristal ×1

**Ni Forêt, ni Plaine, ni Eau.** Sans bois, un départ souterrain est mort-né. Le triangle demandé
couvre l'intégralité de l'économie de base :

| Terrain | Bâtiment | Ressource | Référence |
|---|---|---|---|
| Caverne aux champignons | Ferme à champignons | Nourriture | `Model/Buildings/MushroomFarm.cs:25` |
| Caverne aux champignons | Scierie | **Bois** (½ vitesse) | via `UNLOCK_SAWMILL_MUSHROOM_HARVEST` |
| Colline | Briqueterie | Brique | `Model/Buildings/Brickworks.cs:29` |
| Montagne | Carrière | Pierre | `Model/Buildings/Quarry.cs:25` |
| Montagne | Mine | Minerai | `Model/Buildings/Mine.cs:19` |

Le bois dépend donc entièrement du kit de recherches (§4) : les deux morceaux ne sont pas séparables.

---

## 4. Le kit de recherches offert

Les elfes noirs commencent avec **`Speleologie`** et **`BoisDeChampignon`** déjà complétées.

### 4.1 Nouvelle catégorie `STARTING_RESEARCH` — ✅ fait

- Nouvelle `Modifier.ECategory.STARTING_RESEARCH`, `SubCategory` = nom du `TechnologyId`.
- Consommée par `PrestigeMapController.ApplyStartingResearch`, appelée depuis
  `ApplyPrestigeToNewGame` — **avant** le court-circuit `if (purchased.Count == 0) return;`, sans quoi
  un kit racial dépendrait d'un achat de vertex de prestige qui n'a rien à voir avec lui.
- Idempotente : une recherche déjà acquise est ignorée, donc le kit se ré-applique sans effet de bord
  à chaque début d'île (nouvelle partie, prestige, ascension, restart).

### 4.2 Pourquoi ces deux recherches précisément

| Recherche | Tier / coût | Prérequis normaux | Effet |
|---|---|---|---|
| `Speleologie` | 4 / 23 000 | aucun (débloquée par le vertex Mine Profonde) | Mine +25% vitesse, +10 stockage avancé |
| `BoisDeChampignon` | 6 / 380 000 | `CultureFongique` | `UNLOCK_SAWMILL_MUSHROOM_HARVEST` |

**Astuce volontaire** : les prérequis ne gardent que le *lancement* d'une recherche, pas sa
complétion (`TechnologyTree.CompleteResearch` ajoute sans vérifier). On offre donc les tiers 4 et 6
**sans** offrir `CultureFongique` qui est entre les deux. Elle reste à chercher, et son bonus
(`MushroomFarm` +25% production, +0.25 vitesse) devient l'objectif économique précoce naturel de la
race — d'autant qu'elle est justement débloquée par le vertex de prestige offert (§5).

---

## 5. Le vertex de prestige offert — ✅ fait

Les elfes noirs commencent chaque cycle avec **`PrestigeMap.MushroomCultureVertex`** acquis.

`AscensionController.GrantFreePrestigeVertices` prend désormais la `RaceId` en paramètre (passée
explicitement depuis `PerformAscension` plutôt que relue depuis `SelectedRace`, pour ne pas dépendre
de l'ordre d'affectation) et ajoute les `RaceDefinition.FreePrestigeVertices` à `PurchasedVertices`,
toujours sans coût ni contrainte de contiguïté.

Ce que le vertex apporte (`Model/Prestige/PrestigeMap/PrestigeMapFactory.cs:226`) :

- `BUILDING_MAX_LEVEL MushroomFarm +2` — la ferme à champignons, seule source de nourriture
  souterraine, démarre débridée ;
- `UNLOCK_RESEARCH CultureFongique` — rend pêchable la recherche volontairement omise du kit.

Les deux morceaux s'emboîtent : le kit donne le bois, le vertex donne la nourriture et pointe vers
la recherche suivante.

> **Note d'implémentation** : `GrantFreePrestigeVertices` n'est appelé que depuis `PerformAscension`,
> qui construit un `PrestigeState` neuf. C'est le bon endroit — les vertex offerts persistent ensuite
> sur tout le cycle d'ascension, prestiges compris.

---

## 6. Le Monument : la Percée de Surface — ✅ fait

C'est le miroir exact de la Mine Profonde. Les elfes noirs ne subissent pas leur enfermement : ils
creusent vers le haut.

### 6.1 Comportement

| Aspect | Valeur |
|---|---|
| Nom | Percée de Surface (`SurfaceBreach`) |
| Type | `Monument` (comme `DeepestMine`, `CorruptionSpire`, `AbyssGate`) |
| Placement | Hex **Montagne de l'Inframonde** adjacent à une ville du joueur, sans autre feature, hors zone ennemie |
| Déblocage | **Aucun nouveau modifier.** La Percée est plaçable exactement quand elle a un sens : le joueur n'a aucune ville en surface **et** un `ArrivalVertex` de surface est mémorisé. Cette conjonction ne se produit que pour une race démarrant sous terre (ou après une perte de la surface) — voir `SurfaceBreachController.HasSurfaceBreachUnlocked` |
| Creusement | Investissement progressif via `MonumentInvestment.ProcessTick`, intervalle `MonumentInvestment.IntervalTicks` |
| Coût | Miroir de `DeepestMine.GetDigCost()` : Pierre 1 000 / Minerai 2 000 / Or 2 000 — **levier d'équilibrage principal** |
| À la complétion | Pose la première ville de surface du joueur sur le vertex mémorisé en §3.2, recalcule la visibilité, bascule la vue sur la surface, écrit dans l'`EventLog` |

### 6.2 Pourquoi réutiliser `LayerState.ArrivalVertex`

Le vertex de départ de surface est déjà calculé par `EnsureStartPairNearEdge` au moment de la
génération, avec toutes ses garanties (bord d'île, terrain racial + Forêt + Eau). Le mémoriser plutôt
que de le recalculer évite de dupliquer cette logique, et le champ `ArrivalVertex` de `LayerState`
existe déjà pour exactement cette sémantique.

Conséquence agréable : **le joueur elfe noir débouche sur la surface au même endroit que les autres
races**, sur un vertex Colline/Forêt/Eau — donc avec accès immédiat au bois « normal », à la Ferme et
au Port. L'arrivée en surface est une vraie bascule économique, pas un simple élargissement de carte.

### 6.2bis Le Site d'Arrivée — réserver plutôt que chercher

**La génération protège le point de chute, la suite de la partie non.** `MinDistanceFromPlayer`
(10 arêtes par défaut) s'applique au placement initial des PNJ *et* à leur pré-expansion, via
l'ancrage du §3.2 : à la naissance de l'île, aucune ville adverse ne peut s'en approcher. Mais
l'expansion des PNJ **en cours de partie** (`NpcGameController`) ne pose aucun filtre de distance au
joueur — et un elfe noir peut rester enfermé très longtemps. Une ville adverse pouvait donc venir
s'asseoir exactement sur le vertex d'arrivée, et `TryEstablishSurface` y fondait la ville du joueur
sans vérifier l'occupation : deux villes empilées sur un même vertex, que `WorldState.FindCityAt`
(un `FirstOrDefault`) départage au hasard.

La réponse n'est pas de chercher un point de chute de repli au dernier moment — un tel repli peut
ne pas exister, et déplace silencieusement l'arrivée du joueur. **On réserve la place dès la
génération**, avec un nouveau type d'emplacement :

| Aspect | `Model/Civilization/LandingSite.cs` |
|---|---|
| Nature | `IBuildVertex`, sans bâtiment ni garnison — un marqueur, pas une structure |
| Occupation | Entre dans `Civilization.BuildVertices`, donc dans `WorldState.GetAllBuildVertices` : villes, avant-postes, Balises Maritimes et Camps Mobiles le voient occupé, sans qu'aucun de ces contrôleurs ait à le connaître |
| Distance | Ajouté aux rayons interdits de `CityBuilderController.GetBuildableVertices` **au même titre qu'une ville** — donc rien ne se pose dessus ni à distance 1 |
| Inattaquable | **Volontairement pas** un `IMilitaryVertex` : absent de `Civilization.MilitaryVertices`, donc invisible pour le système militaire ; et `MonsterController.FindAttackTarget` ne parcourt que `Cities`. Ni PNJ ni monstres ne peuvent le viser ou le détruire |
| Cycle de vie | Posé par le générateur en même temps que `ArrivalVertex` ; cédé à la ville par `TryEstablishSurface` ; **re-réservé** par `ResetSurfaceAfterLastCityDestroyed`, sans quoi la place serait libre pendant tout le re-creusement — précisément la fenêtre où un PNJ s'y installerait |

Le compteur de sites entre aussi dans la clé du cache de `GetBuildableVertices` : ses mutations
coïncident toujours avec un changement du nombre de villes, mais s'appuyer sur cette coïncidence
serait fragile — c'est exactement le piège que `RelocateCity` avait dû corriger par un vidage
explicite du cache.

Tests : `LandingSite_ReservesTheArrivalVertexWhileLockedUnderground`,
`LandingSite_IsNeverAMilitaryTarget`, `LandingSite_BlocksCityPlacementOnItAndAtDistanceOne`,
`LandingSite_IsReleasedWhenTheSurfaceCityIsFounded`, `LandingSite_SurvivesASaveRoundTrip`.

### 6.3 Perte de la surface

Miroir de `DeepestMineController.ResetUnderworldAfterLastCityDestroyed` : si la dernière ville de
surface du joueur est détruite, la Percée retombe à **50 % d'investissement**, la vue rebascule sur
l'Inframonde, et le vertex d'arrivée reste mémorisé pour rouvrir au même endroit.

**Une divergence assumée avec la Mine Profonde.** Perdre l'Inframonde y détruit la couche entière
(carte vidée, features et civs NPC souterraines supprimées) : légitime, cette couche n'existait que
pour le joueur. La surface, elle, **porte toute l'île** — les NPC, les features, les volcans. La
`SurfaceBreachController` ne vide donc rien : elle retire seulement les routes de surface du joueur,
devenues orphelines. Le monde continue de tourner sans lui, et c'est ce qu'il retrouvera en
ressortant. Couvert par le test `LastSurfaceCityDestroyed_KeepsSurfaceMapAndItsOtherCivilizations`.

### 6.4 Fichiers — ✅ tous faits

| Fichier | Nature |
|---|---|
| `Model/Civilization/LandingSite.cs` | Nouveau — réservation du point de chute (voir §6.2bis) |
| `Model/Civilization/Civilization.cs` | Liste `LandingSites` (sérialisée) ajoutée à `BuildVertices`, jamais à `MilitaryVertices` |
| `Controller/Island/CityBuilderController.cs` | Sites d'Arrivée intégrés aux rayons interdits + à la clé du cache |
| `Model/IslandFeatures/SurfaceBreach.cs` | Nouveau, calqué sur `DeepestMine.cs` |
| `Model/IslandFeatures/IslandFeature.cs` | `[JsonDerivedType(typeof(SurfaceBreach), "SurfaceBreach")]` |
| `Model/Game/GameEventLog.cs` | 3 valeurs ajoutées en fin d'enum : `SurfaceBreachPlaced`, `SurfaceBreachDug`, `SurfaceLost` |
| `Controller/Expand/SurfaceBreachController.cs` | Nouveau, calqué sur `DeepestMineController.cs` |
| `Controller/MainGameController.cs` | Propriété, instanciation, `Initialize`, et branchement sur `OnCityDestroyedHandler` |
| `Controller/CivilizationAutoplayer.cs` | `TrySurfaceBreachInvestmentOnce()` + dépendance optionnelle |
| `SOIStrategyTester/StrategyRunner.cs` | Appel inconditionnel dans `TryAdvanceBackgroundSystemsOnce` |

---

## 7. Le pouvoir : le Pacte des Profondeurs — ✅ fait

**Trolls et ogres n'attaquent jamais les villes des elfes noirs.**

### 7.1 Nouvelle catégorie `MONSTER_ATTACK_IMMUNITY`

`Modifier.ECategory.MONSTER_ATTACK_IMMUNITY`, `SubCategory` = nom de la classe du monstre
(`"Troll"`, `"Ogre"`), vide = tous les monstres. Deux modifiers portés par la race.

Point d'accroche : `MonsterController.FindAttackTarget` — les deux boucles de recherche de cible
sautent une civilisation entière via `IsImmuneTo(civ, monster)`, comparé à `monster.GetType().Name`.
Filtrer là plutôt que dans `ApplyMonsterAttack` évite que le monstre affiche un `LastAttackTargetVertex`
fantôme ; c'est précisément ce que vérifie le test `Troll_WithImmunity_NeverTargetsTheCity`.

Le précédent exact est juste à côté : le court-circuit Palissade dans `ApplyMonsterAttack` (`:491`).

### 7.2 Pourquoi ce n'est pas un free pass

Trolls et ogres conservent `BlocksHarvest = true` (`Model/Monsters/Troll.cs:22`,
`Model/Monsters/Ogre.cs:22`) : ils continuent de stériliser les hexes qu'ils occupent. Ils ne pillent
plus, ils **encombrent**. Et l'immunité ne couvre pas les autres habitants de l'Inframonde (rats,
bandits, démons mineurs et majeurs, aventuriers hostiles).

Le pouvoir supprime la pression militaire précoce — critique quand on démarre sans surface, sans
recul et sans Tour de Guet (`Watchtower.IsAvailableInLayer` exclut l'Inframonde) — sans supprimer le
besoin de nettoyer la carte pour récolter.

---

## 8. Le bâtiment racial : le Sanctuaire de l'Araignée — ✅ fait

Obligatoire pour rendre la race sélectionnable (§2). Disponible à partir du niveau de ville 3
(`AvailableAtLevel = 3`), implémente `IUniqueBuilding` pour émettre ses modifiers.

| Aspect | Valeur |
|---|---|
| `BuildingType` | `SpiderShrine` |
| Clés | `building_spidershrine_name` / `_desc` |
| Disponibilité | Souterrain uniquement — `IsAvailableInLayer(z) => z != IslandMap.SurfaceLayer`, même patron que `AdventurersGuild.cs:26` |
| Unicité | `IsUnique => true`, `GetDefaultMaxLevel() => 0` + `BUILDING_MAX_LEVEL SpiderShrine +1` par la race (patron standard des uniques) |
| Effet | *(changé depuis)* Réduit de 1 les dégâts de toute attaque de monstre visant une ville de la civilisation (`MONSTER_DAMAGE_REDUCTION_ON_CITIES`) — remplace l'immunité `MONSTER_ATTACK_IMMUNITY` initialement accordée sur Rats et Démons mineurs |

L'effet prolonge la fantasy raciale au lieu d'ajouter un bonus économique générique : le sanctuaire
achève ce que le Pacte commence.

**Repli si l'immunité étendue paraît trop forte** : `UNDERWORLD_MONSTER_SPAWN_INTERVAL +0.75`
(la catégorie existe, utilisée par la recherche `VeilleSouterraine`).

Les 5 touch points d'ajout de bâtiment sont ceux du `CLAUDE.md` (enum, classe, converter, factory,
localisation).

---

## 9. Définition finale de la race — ✅ en place

Telle qu'écrite dans `RaceDefinitions.cs` (les 3 nouveaux paramètres suivent `modifiers`, qui reste
le dernier paramètre obligatoire) :

```csharp
new RaceDefinition(RaceId.DarkElf, RaceTier.Advanced,
    requiredAdjacentTerrain: null,
    racialBuilding: BuildingType.SpiderShrine,
    modifiers: new[]
    {
        new Modifier(ECategory.STARTING_RESEARCH, nameof(TechnologyId.Speleologie), EType.ADDITIVE, 1),
        new Modifier(ECategory.STARTING_RESEARCH, nameof(TechnologyId.BoisDeChampignon), EType.ADDITIVE, 1),
        new Modifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Troll), EType.ADDITIVE, 1),
        new Modifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Ogre), EType.ADDITIVE, 1),
        new Modifier(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.SpiderShrine), EType.ADDITIVE, 1),
    },
    startsInUnderworld: true,
    underworldStartTerrains: new[] { TerrainType.MushroomCave, TerrainType.Hill, TerrainType.Mountain },
    freePrestigeVertices: new[] { PrestigeMap.MushroomCultureVertex }),
```

> `startsInUnderworld`, `underworldStartTerrains` et `freePrestigeVertices` sont trois nouveaux
> paramètres optionnels de `RaceDefinition` — aucune autre race n'est touchée. Le triangle vit sur la
> race plutôt que dans le générateur : une future race souterraine choisira son propre trio.

---

## 10. Récapitulatif des touch points

### Cœur — ✅ fait

| Fichier | Changement |
|---|---|
| `Model/Races/RaceDefinition.cs` | + `StartsInUnderworld`, + `UnderworldStartTerrains`, + `FreePrestigeVertices` |
| `Model/Races/RaceDefinitions.cs` | Stub remplacé par la définition du §9 |
| `Model/IslandMap/LayerState.cs` | Terrains du triangle paramétrables (`triangleTerrains`) |
| `Controller/Generator/IslandMapGenerator.cs` | `populatePlayerCity`, `LastSurfaceStartVertex`, `race:` à la place de `startVertexTerrain`, `EstablishUnderworldStart`, `GetSurfaceAnchorVertex` |
| `Controller/Generator/NpcCivilizationPlacer.cs` | + `playerAnchorVertex` (voir §3.2) |
| `Model/GameplayModifier/Modifier.cs` | + `STARTING_RESEARCH`, + `MONSTER_ATTACK_IMMUNITY` |
| `Controller/Expand/PrestigeMapController.cs` | `ApplyStartingResearch` |
| `Controller/Ascension/AscensionController.cs` | Vertex raciaux dans `GrantFreePrestigeVertices`, appel `race:` du générateur |
| `Controller/Expand/PrestigeController.cs`, `Controller/MainGameController.cs` | Appels `race:` du générateur |
| `Controller/Military/MonsterController.cs` | `IsImmuneTo` dans `FindAttackTarget` |

### Percée de Surface — ✅ fait

Voir §6.4.

### Bâtiment racial — ✅ fait

`Model/Buildings/Building.cs` (enum), `Model/Buildings/SpiderShrine.cs`,
`Model/Buildings/BuildingJsonConverter.cs`, `Controller/Island/BuildingController.cs`
(`CreateBuilding`).

> Le `CLAUDE.md` mentionne « 2 emplacements » dans le converter : il n'y en a en réalité qu'un
> (le `switch` de `Read`), `Write` sérialisant par type runtime.

### Localisation — ✅ fait

`Resources/Localization/fr.json` + `en.json` : `race_darkelf_desc` réécrite,
`building_spidershrine_name`/`_desc`, `surface_breach_panel_title`,
`hex_tooltip_surface_breach`/`_dug`, et les 3 paires `event_surface_*_title`/`_body`.

### Tests — ✅ fait

| Fichier | Contenu |
|---|---|
| `SOITests/ControllerTests/DarkElfRaceTests.cs` | Nouveau — définition de race, départ souterrain (E2E via `PerformAscension`), non-régression des autres races, kit de recherches, vertex offert, Pacte des Profondeurs, Sanctuaire |
| `SOITests/ControllerTests/SurfaceBreachControllerTests.cs` | Nouveau — déblocage, placement, percement, fondation de la ville de surface, perte et réouverture, réservation du point de chute |
| `SOITests/ControllerTests/RaceSystemTests.cs` | Assertion « jamais sélectionnable » inversée (voir §2) |

---

## 11. Risques et points à vérifier

1. ⬜ **Viabilité d'une partie souterraine prolongée.** Merveilles, volcans, Temples/Dominion, Grand
   Phare, Hutte d'Alchimie et Ziggourat sont liés à la surface. Non mesuré : à faire tourner dans
   `SOIStrategyTester` avant de figer le coût de la Percée (aujourd'hui strictement égal à celui de
   la Mine Profonde : Pierre 1 000 / Minerai 2 000 / Or 2 000), c'est lui qui détermine combien de
   temps la race reste enfermée.
2. ✅ **Pression militaire précoce — traité.** Le risque était pire que prévu : sans point d'ancrage,
   `PlaceNpcCivilizations` sortait sur `Cities.Count == 0` et l'île naissait vide d'adversaires. Réglé
   par l'ancrage de surface (§3.2). Reste à jauger à l'usage si les civs agressives de la couche
   auto-extend suffisent à créer une menace *souterraine* pendant l'enfermement.
3. ✅ **Sérialisation.** Les 2 nouvelles `ECategory` héritent du `JsonStringEnumConverter` de
   `Modifier.ECategory` ; les 3 `GameEventType` sont ajoutées en fin d'enum (et l'`EventLog` n'est de
   toute façon pas persisté) ; `SurfaceBreach` porte son `[JsonDerivedType]` et
   `SpiderShrine` son entrée de converter.
4. ✅ **Autoplay et StrategyTester.** `CivilizationAutoplayer.TrySurfaceBreachInvestmentOnce` +
   appel inconditionnel dans `StrategyRunner.TryAdvanceBackgroundSystemsOnce`, à côté de la Mine
   Profonde.
5. ⬜ **Interaction avec l'Abysse.** Non vérifié. `AbyssGateController` ouvre la couche 2 depuis la
   surface ; un joueur elfe noir n'ayant jamais percé n'a simplement pas accès à la chaîne Abysse,
   ce qui est cohérent mais n'a pas été testé de bout en bout.

---

## 12. Ordre d'implémentation — suivi tel quel

1. ✅ **Socle race** — `StartsInUnderworld`, triangle paramétrable, départ souterrain, `SpiderShrine`.
2. ✅ **Kit de démarrage** — `STARTING_RESEARCH` + vertex de prestige offert.
3. ✅ **Pacte des Profondeurs** — `MONSTER_ATTACK_IMMUNITY`.
4. ✅ **Percée de Surface** — le Monument et son contrôleur.
5. ✅ **Autoplay, StrategyTester, localisation, tests.** ⬜ **UI** — voir §13.

La séparation annoncée s'est vérifiée : les étapes 1 à 3 ont été testées avant qu'une seule ligne du
Monument ne soit écrite.

---

## 13. Reste à faire — UI (Skia)

Volontairement hors périmètre de cette passe. **Rien ne plante sans**, les deux `switch` concernés
ayant un cas par défaut :

| Fichier | Manque | Effet actuel |
|---|---|---|
| `Renderers/Overlay/Tabs/EventLogRenderer.cs` | 3 cas (`SurfaceBreachPlaced`, `SurfaceBreachDug`, `SurfaceLost`) | Les entrées s'affichent avec le titre `"?"` — les clés de localisation `event_surface_*` sont déjà écrites, il n'y a que le câblage à faire |
| `Renderers/Overlay/Panels/PlayerCivilizationPanelRenderer.cs` | Panneau d'investissement de la Percée (calquer la Mine Profonde, `:737` et `:1073`) | Le joueur ne peut pas piloter l'investissement |
| `Services/ConstructionInteractionService.cs` | Placement de la Percée | Le joueur ne peut pas poser le Monument — **c'est le seul manque bloquant pour jouer la race à la main** ; l'autoplay, lui, sait déjà le faire |
| Rendu de la carte (vertex) | Icône du Site d'Arrivée en surface | Le point de chute réservé est invisible : le joueur ne sait pas où il débouchera. Clés `landing_site_name` / `landing_site_tooltip` déjà écrites |
| `Renderers/Overlay/Tabs/PrestigeMapRenderer.cs` | Cas `FormatModifier()` pour les 2 nouvelles `ECategory` (règle du `CLAUDE.md`) | Aucun : ces catégories ne sont portées que par la race et le Sanctuaire, jamais par un vertex ou un hex de prestige — `FormatModifier` ne les rencontre pas. À ajouter par principe si l'une d'elles atterrit un jour sur la carte de prestige |

> ⚠️ Le dépôt migre vers Avalonia (branche `Avalonia`, projet `SettlersOfIdlestanUI`). Ce travail UI
> est donc à faire sur la branche `Avalonia`, pas ici — le refaire en Skia sur `main` serait jeté.
