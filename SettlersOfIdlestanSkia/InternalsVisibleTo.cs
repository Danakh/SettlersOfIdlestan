using System.Runtime.CompilerServices;

// Donne à SOIUITests l'accès aux membres internes du rendu — voir
// PrestigeMapRendererFormatModifierTests, qui balaie les 121 valeurs de Modifier.ECategory.
[assembly: InternalsVisibleTo("SOIUITests")]
