# Document de Game Design — Conversion vers C#/Blazor (FR)

Version : 0.1
Date : 29 janvier 2026
Projet source : `ColonsOfIdlestan` (prototype TypeScript)
Objectif : Décrire la conversion du prototype TypeScript en une application C# moderne avec UI Blazor.

## Objectif du document
Présenter les décisions de conception, l'architecture technique, le mapping des modèles et des algorithmes, et un plan de migration itératif pour porter le jeu vers .NET (librairie C# réutilisable) et `Blazor` pour l'interface.

## Portée
- Porter la logique de jeu (règles, génération, état) vers une bibliothèque `.NET` (`Colons.Core`).
- Créer un client UI en `Blazor WebAssembly` (`Colons.Client`) ; option `Blazor Server` si besoin d'autorité serveur.
- Fournir un backend optionnel `Colons.Server` (ASP.NET Core) avec `SignalR` pour le multi-joueur et la persistance.
- Tests unitaires et d'intégration en C# (`xUnit`).

## Principes de conception
- Séparer clairement la logique métier (`Colons.Core`) de la présentation (`Colons.Client`).
- Favoriser les types forts : `record`, `enum`, `record struct` pour les value objects.
- Injection de dépendances pour les services (ex : `IRandom`, `IClock`) afin de faciliter les tests.
- Le serveur (si utilisé) est authoritative ; le client n'est qu'un rendu et un proxy d'input.

## Structure de solution proposée
- `Colons.Core` (class library)
  - Modèles de domaine : `GameState`, `Player`, `HexTile`, `City`, `Resource`.
  - Algorithmes : `MapGenerator`, `HexAlgorithms`, `Pathfinding`.
  - Interfaces : `IRandom`, `IGameClock`, `IStateSerializer`.
  - Tests unitaires ciblant ce projet.

- `Colons.Server` (ASP.NET Core)
  - Hub SignalR : `GameHub` (événements : `Join`, `Leave`, `StartGame`, `PlayerAction`, `StateUpdate`).
  - Points d'API pour sauvegarde/chargement et listings de parties.
  - Persistence (optionnel) : `EF Core` ou stockage JSON simple.

- `Colons.Client` (Blazor WASM)
  - Composants UI, services réseau, store d'état local.
  - Rendu du plateau : `Board.razor` (SVG ou Canvas), `HexTile.razor`, `PlayerPanel.razor`.

- `Colons.Tests` (xUnit) et `Colons.E2E` (Playwright for .NET)

## Mapping TypeScript -> C# (règles rapides)
- `interface`/`type` -> `record` ou `class` selon mutabilité.
- `number` -> `int` ou `double` ; `Array<T>` -> `List<T>` / `T[]`.
- Fonctions pures -> méthodes statiques ou services.
- Aléatoire testable via `IRandom` injectable.

Exemples :
- `Player` TS -> `public record Player(Guid Id, string Name, int Score);`
- `HexCoordinate` TS -> `public readonly record struct HexCoordinate(int Q, int R);`

## Contrats et sérialisation
- Utiliser `System.Text.Json` pour sérialiser `GameState`.
- Ajouter attributs `[JsonConstructor]`, `[JsonPropertyName]` si nécessaire.
- Versionner `GameState` : inclure un champ `Version` pour migrations.
- Exemple de DTOs : `GameStateDto`, `PlayerActionDto`, `StatePatchDto`.

## Synchronisation et modèle réseau
- Mode solo : logique exécutée côté client en local.
- Mode multi : server-authoritative via `Colons.Server` + `SignalR`.
- Flux d'événements :
  - Client envoie `PlayerAction` au serveur.
  - Serveur valide, applique règle via `Colons.Core`, publie `StateUpdate` (diffs ou snapshot).
  - Client applique l'état reçu.
- Considérer envoi de patches minimaux (diffs) pour réduire bande passante.

## Design UI Blazor
- Composants clés : `Board.razor`, `HexTile.razor`, `CityPanel.razor`, `Lobby.razor`, `GameOverlay.razor`.
- Rendu : SVG pour facilité d'interaction et scalabilité ; Canvas pour grandes cartes et performances.
- State management : utiliser un store simple (`Scoped` service) ou `Fluxor` pour pattern Redux-like.
- Eviter de rerendre le DOM entier : utiliser `@key` et composants séparés.

## Input & UX
- Clic / context menu pour construire/inspecter.
- Drag to pan, wheel to zoom, double-click to recentre.
- Indicateurs réseau : `Connecting`, `Syncing`, `OutOfSync`.

## Tests
- Unitaires : `Colons.Core` règles et algorithmes avec `IRandom` contrôlé.
- Intégration : scenarios de parties en mémoire (server + multiple clients simulés).
- E2E : Playwright pour vérifier parcours UI (construction, sauvegarde, chargement).

## Performance
- Garder `Colons.Core` déterministe et rapide (éviter allocations fréquentes pendant ticks).
- Pour rendu lourd, utiliser Canvas + batching des draw calls.
- Déplacer traitements lourds (ex: génération) sur Task / server si nécessaire.

## Sécurité
- Valider côté serveur toutes les actions reçues.
- Authentification optionnelle (JWT ou cookie auth) pour parties persistées.

## Plan de migration (phases)
1. Inventaire TS: lister types, fonctions clés et tests existants (`ColonsOfIdlestan/src`).
2. Créer solution .NET et projets (`Colons.Core`, `Colons.Client`, `Colons.Server`, `Colons.Tests`).
3. Porter DTOs, `HexCoordinate`, `HexAlgorithms` et `MapGenerator` dans `Colons.Core`.
4. Écrire tests unitaires pour chaque règle portée.
5. Implémenter composant `Board.razor` minimal et afficher un plateau statique depuis `Colons.Core`.
6. Ajouter `SignalR` et test server-authoritative minimal (join/start/action).
7. Itérer UI, optimisation et E2E.

## Timeline estimée (itératif)
- Semaine 1: setup solution, porter modèles de base.
- Semaine 2-3: porter map generator et tests.
- Semaine 4-6: UI Blazor basique, affichage plateau et interactions locales.
- Semaine 7-9: SignalR, multiplayer, persistance.
- Semaine 10-12: tests E2E, polissage, packaging.

## Checklist technique immédiate
- [ ] Créer `Colons.Core` et y ajouter `IRandom`, `HexCoordinate`, `HexGrid`.
- [ ] Ajouter tests unitaires pour `HexAlgorithms`.
- [ ] Créer `Colons.Client` avec `Board.razor` et service `GameStateService`.
- [ ] Ajouter `Colons.Server` avec `GameHub` SignalR minimal.

## Annexes & références
- Fichiers TS à analyser : `src/view/HexMapRenderer.ts`, `src/controller/MapGenerator.ts`, `src/model/hex/HexCoord.ts`.
- Packages recommandés : `Microsoft.AspNetCore.SignalR`, `Fluxor` (optionnel), `Playwright` for .NET.

---
Fichier créé: `ColonsOfIdlestan/docs/GameDesignDocument_Conversion_CSharp_Blazor_FR.md`
