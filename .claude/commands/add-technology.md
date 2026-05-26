Ajoute une nouvelle technologie (recherche en jeu) dans SettlersOfIdlestan.

Arguments attendus (fournis par l'utilisateur sous forme libre) :
- Nom de la technologie (identifiant C#, ex: `Artisanat`)
- Coût en points de recherche (ex: 100)
- Prérequis technologiques (ex: `Architecture`, ou aucun)
- Effet / modificateur (ex: `FORGE_DOUBLE_PROD_BONUS +5`, `HARVEST_SPEED +0.1`, `BUILDING_MAX_LEVEL "TownHall" +1`…)
- Vertex prestige requis pour débloquer (ex: `AppliedResearchVertex`, ou aucun si toujours disponible)

Fichiers à modifier dans l'ordre :

1. **`SettlersOfIdlestan/Model/Civilization/Technology.cs`**
   - Ajouter `<Nom>` à l'enum `TechnologyId`.

2. **`SettlersOfIdlestan/Model/Civilization/TechnologyDefinitions.cs`**
   - Ajouter une entrée `new(TechnologyId.<Nom>, "tech_<nom>_name", "tech_<nom>_desc", cost: X, prerequisites: ..., modifiers: ...)` dans le tableau `All`.

3. **`SettlersOfIdlestan/Model/GameplayModifier/Modifier.cs`** *(si nouveau modificateur)*
   - Ajouter la valeur à `ECategory` si l'effet utilise une catégorie inexistante.

4. **`SettlersOfIdlestan/Model/Civilization/Civilization.cs`** *(si nouveau modificateur)*
   - Ajouter une propriété `[JsonIgnore] public <type> <NomBonus> => ModifierAggregator.ApplyModifiers(ECategory.<CATÉGORIE>, "", <valeur_base>);` sur le modèle des propriétés existantes (`ResearchSpeed`, `ResearchCostReduction`…).

5. **Contrôleur concerné** *(si nouveau modificateur)*
   - Appliquer le bonus via `civ.<NomBonus>` à l'endroit approprié (ex: `HarvestController` pour un bonus de récolte).

6. **`SettlersOfIdlestan/Controller/Expand/ResearchController.cs`** *(si vertex prestige requis)*
   - Ajouter un cas dans `IsPrestigeRequirementMet()` :
     ```csharp
     TechnologyId.<Nom> => _prestigeState?.PurchasedVertices.Contains(PrestigeMap.<VertexStatique>) == true,
     ```

7. **`SettlersOfIdlestan/Resources/Localization/fr.json`**
   - Ajouter `"tech_<nom>_name"` et `"tech_<nom>_desc"`.

8. **`SettlersOfIdlestan/Resources/Localization/en.json`**
   - Ajouter `"tech_<nom>_name"` et `"tech_<nom>_desc"`.

Après toutes les modifications, compiler avec :
```
dotnet build SOITests/SOITests.csproj -v q
```
et vérifier l'absence d'erreurs.

Rappels architecture :
- Les modificateurs sont appliqués via `ModifierAggregator` (TechnologyTree + Prestige + autres providers enregistrés dans `MainGameController`).
- `TechnologyTree.RebuildModifiers()` reconstruit les modificateurs à partir de `CompletedTechnologies` — aucune autre mise à jour manuelle nécessaire.
- Une technologie avec `RequiredPrestigeVertex` apparaît `Inactive` dans l'UI tant que le vertex n'est pas acheté (géré par `ResearchController.GetStatus()`).
