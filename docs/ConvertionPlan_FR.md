# Plan de conversion pas-à-pas depuis TypeScript vers C#/Blazor (FR)

Ce document décrit une feuille de route pratique et testable pour convertir le prototype TypeScript de `ColonsOfIdlestan` en une solution .NET + Blazor. Chaque étape précise les objectifs, les fichiers/éléments à porter, et des tests unitaires ou d'intégration pertinents à ajouter.

Pré-requis
- .NET 10 SDK installé
- Connaissance basique de TypeScript et des sources du prototype
- Outils recommandés : Visual Studio/VS Code, dotnet CLI, Playwright (pour E2E)

Commandes utiles (exemples)
- Créer solution et projets:
  - `dotnet new sln -n ColonsOfIdlestan`
  - `dotnet new classlib -n Colons.Core`
  - `dotnet new blazorwasm -n Colons.Client`
  - `dotnet new webapi -n Colons.Server`
  - `dotnet new xunit -n Colons.Tests`
  - `dotnet sln add **/*.csproj`
- Ajouter packages:
  - `dotnet add Colons.Server package Microsoft.AspNetCore.SignalR`
  - `dotnet add Colons.Core package System.Text.Json`
  - `dotnet add Colons.Tests package Microsoft.NET.Test.Sdk xunit` (dotnet template le gère déjà)

Étapes de conversion

Étape 0 — Inventaire et tests de comportement (durée: 1-2 jours)
- Objectif: identifier les types, fonctions, règles et scénarios tests existants dans le code TS.
- Actions:
  - Lister fichiers TS importants: `HexCoord`, `MapGenerator`, `HexMapRenderer`, controllers (Resource/Prestige).
  - Exécuter et noter comportements observables depuis le prototype (sauvegardes exemple dans `assets/saves/`).
- Tests à écrire:
  - Tests d'oracle basés sur sauvegardes TS: charger une sauvegarde TS et enregistrer l'état attendu (JSON) pour validations futures.

Étape 1 — Initialiser la solution .NET et projet `Colons.Core` (durée: 1 jour)
- Objectif: mettre en place la structure, CI basique et le projet core.
- Actions:
  - Créer solution et projets (voir commandes).
  - Configurer `Colons.Core` comme librairie contenant types et algos.
- Tests à écrire:
  - Test de création de solution: build passe.

Étape 2 — Porter les value objects et utilitaires hexagonaux (HexCoordinate, HexDirections) (durée: 2-3 jours)
- Objectif: porter les types de coordonnés et directions hexagonales.
- Fichiers à ajouter:
  - `HexCoordinate.cs` (record struct immuable)
  - `HexDirections.cs` (enum/array directions)
- Tests à écrire (xUnit):
  - `HexCoordinateTests.CanComputeNeighbor` : vérifier que la méthode `GetNeighbor(direction)` retourne la coordonnée attendue pour chaque direction (comparer aux valeurs du prototype).
  - `HexCoordinateTests.SerialisationRoundtrip` : sérialiser/désérialiser avec `System.Text.Json` et comparer.

Étape 3 — Porter `HexGrid` et `HexAlgorithms` (distance, range, line) (durée: 3-5 jours)
- Objectif: porter les algorithmes de voisinage, distance et trajectoires.
- Fichiers à ajouter:
  - `HexGrid.cs` (construction de grille, itérateurs)
  - `HexAlgorithms.cs` (distance, ring, line)
- Tests à écrire:
  - `HexAlgorithmsTests.DistanceMatchesPrototype` : pour plusieurs paires de coords, vérifier la distance.
  - `HexAlgorithmsTests.LineAndRing` : générer line/ring et comparer aux séquences attendues extraites du prototype.

Étape 4 — Porter `MapGenerator` (génération procédurale seedée) (durée: 4-7 jours)
- Objectif: porter la génération de carte pour obtenir des maps identiques pour un même seed.
- Fichiers à ajouter:
  - `MapGenerator.cs`
  - `SeededRandom` (implémentation conforme à TS RNG ou adapter et rendre testable via `IRandom`).
- Tests à écrire:
  - `MapGeneratorTests.SeededMapIsDeterministic` : deux exécutions avec même seed produisent identiques `MapDescriptor` (hash du JSON).
  - `MapGeneratorTests.ParametersAffectMap` : varier paramètres (water density, size) et vérifier que la distribution change.

Étape 5 — Modèles de domaine: `Player`, `Tile`, `City`, `GameState` et sérialisation (durée: 3-5 jours)
- Objectif: définir le modèle de l'état de jeu et assurer serialization stable.
- Fichiers à ajouter:
  - `Player.cs`, `Tile.cs`, `City.cs`, `GameState.cs` (utiliser `record`/`record struct` lorsque pertinent)
- Tests à écrire:
  - `GameStateTests.SerializationVersioning` : roundtrip JSON et compatibilité de version.
  - `GameStateTests.BasicStateTransitions` : appliquer une séquence d'actions (ex: construire bâtiment) via méthodes purifiées et vérifier le nouvel état.

Étape 6 — Règles et controllers (ResourceHarvest, BuildingProduction, Prestige) (durée: 5-10 jours)
- Objectif: porter la logique métier core, en la séparant en méthodes pures testables.
- Fichiers à ajouter:
  - `ResourceController.cs`, `BuildingController.cs`, `PrestigeController.cs`
- Tests à écrire:
  - `ResourceControllerTests.ProductionPerTick` : simuler ticks et vérifier production & stockage.
  - `BuildingControllerTests.ConstructionRules` : valider contraintes (un bâtiment par type par ville, prérequis).
  - `PrestigeControllerTests.PrestigeGains` : scénarios qui donnent des points de prestige.

Étape 7 — Intégration locale: exécuter une partie complète en mémoire (durée: 3-5 jours)
- Objectif: combiner les controllers et GameState pour exécuter un scenario complet sans UI.
- Tests à écrire (intégration):
  - `GameplayIntegrationTests.SimpleScenario` : démarrer une map, exécuter N ticks, appliquer actions et valider invariants (ressources non négatives, bâtiments créés, prestige attendu).

Étape 8 — Client Blazor minimal (Board renderer) (durée: 4-7 jours)
- Objectif: afficher un plateau statique et permettre sélection d'hex.
- Fichiers à ajouter:
  - `Colons.Client` avec `Board.razor`, `HexTile.razor`, `GameStateService.cs` (Scoped) qui charge `GameState` depuis `Colons.Core` via JSON.
- Tests à écrire:
  - Tests unitaires sur services cliente (mock `HttpClient`): `GameStateServiceTests.LoadsState`.
  - E2E basique (Playwright): `ClientE2ETests.CanRenderBoardAndSelectHex`.

Étape 9 — SignalR et mode server-authoritative (durée: 5-10 jours)
- Objectif: ajouter `Colons.Server` avec `GameHub` pour synchroniser clients.
- Fichiers à ajouter:
  - `GameHub.cs`, endpoints de sauvegarde, simple persistence JSON.
- Tests à écrire:
  - `ServerIntegrationTests.HubAppliesActionAndBroadcasts` : simuler client hub calls, vérifier état serveur et messages envoyés.

Étape 10 — E2E & Playwright: scénarios utilisateurs (durée: 4-8 jours)
- Objectif: automatiser parcours clé (créer partie, construire, sauvegarder, charger).
- Tests à écrire:
  - `E2ETests.CreateGame_Build_Save_Load` : vérifier bouton UI, état et persistence.

Étape 11 — Polissage, migration d'assets, optimisation (durée: variable)
- Profilage et optimisation (déplacer génération/IA côté serveur si nécessaire).
- Ajouter CI (GitHub Actions): build, tests unitaires et E2E.

Conseils pratiques et bonnes pratiques
- Rendre les contrôleurs purement fonctionnels autant que possible (retournent nouvel état au lieu de muter globalement).
- Utilities testables: abstraire l'aléatoire (`IRandom`) et l'heure (`IClock`).
- Versionner les DTOs et ajouter migrations de sauvegardes simples.
- Garder tests rapides : préférer tests unitaires et d'intégration simulant le serveur plutôt que tests E2E coûteux en développement initial.

Template de nommage de tests (exemples)
- `ClassUnderTest_Condition_ExpectedResult` ou `FeatureTests.Scenario_ShouldDoX`.
- Grouper tests par dossier: `Colons.Core.Tests/Hex/HexCoordinateTests.cs`, `Colons.Core.Tests/Map/MapGeneratorTests.cs`.

Livrables attendus par milestone
- Milestone 1: solution .NET, `Colons.Core` avec Hex types et algos + tests verts.
- Milestone 2: `MapGenerator` stable avec tests seedés reproduisant cartes.
- Milestone 3: core controllers portés et tests d'intégration.
- Milestone 4: client Blazor minimal et E2E pipeline.

Ce document est conçu pour être utilisé comme checklist exécutable. Je peux :
- Générer les commandes `dotnet` exactes pour créer la solution et projets.
- Créer les projets/csproj + fichiers initialisés (`HexCoordinate`, `IRandom`) automatiquement dans la solution.
- Générer des squelettes de tests xUnit pour chaque étape.

Fichier ajouté: `ColonsOfIdlestan/docs/ConvertionPlan_FR.md`
