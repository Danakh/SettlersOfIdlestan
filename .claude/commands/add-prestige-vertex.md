Ajoute un nouveau vertex de prestige dans la PrestigeMap du jeu SettlersOfIdlestan.

Arguments attendus (fournis par l'utilisateur sous forme libre) :
- Nom du vertex (clé de localisation, ex: `applied_research`)
- Vertex prérequis (ex: `LaboratoryVertex`)
- Coût en points de prestige (ex: 5)
- Modificateurs éventuels (ex: `BUILDING_MAX_LEVEL "Forge" +1`, ou aucun)
- Bâtiments de départ éventuels (ex: `Seaport`, ou aucun)

Fichiers à modifier dans l'ordre :

1. **`SettlersOfIdlestan/Model/Prestige/PrestigeMap/PrestigeMap.cs`**
   - Ajouter la constante `static readonly Vertex <NomVertex> = Vertex.Create(...)` dans la section "Prestige vertices". Le vertex doit être adjacent à son prérequis (partager 2 de ses 3 HexCoords). Choisir un HexCoord libre qui n'entre pas en collision avec les vertex existants.
   - Ajouter l'entrée `new PrestigeVertex(...)` dans `CreateDefault()`.
   - Mettre à jour les listes `adjacentVertices` des `PrestigeHex` existants pour inclure le nouveau vertex dans les hexes qu'il touche géométriquement (ceux dont les 3 HexCoords contiennent 2 des 3 HexCoords du nouveau vertex).

2. **`SettlersOfIdlestan/Resources/Localization/fr.json`**
   - Ajouter `"prestige_vertex_<nom>": "<Nom FR>"`.

3. **`SettlersOfIdlestan/Resources/Localization/en.json`**
   - Ajouter `"prestige_vertex_<nom>": "<Nom EN>"`.

Après chaque modification, compiler avec :
```
dotnet build SettlersOfIdlestan/SettlersOfIdlestan.csproj -v q
```
et vérifier l'absence d'erreurs de compilation.

Rappels architecture :
- `Vertex.Create(h1, h2, h3)` prend 3 `HexCoord` dans l'ordre canonique (les 3 hexes adjacents au vertex).
- Deux vertex sont adjacents si et seulement si ils partagent exactement 2 HexCoords.
- Un vertex apparaît dans `adjacentVertices` de tous les `PrestigeHex` dont la `Coord` fait partie de ses 3 HexCoords.
- Si le vertex débloque une technologie (verrouillée par prestige), ajouter aussi le check dans `ResearchController.IsPrestigeRequirementMet()`.
