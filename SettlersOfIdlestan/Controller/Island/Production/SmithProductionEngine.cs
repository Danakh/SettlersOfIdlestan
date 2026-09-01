using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island.Production;

/// <summary>
/// Forges d'Armes et d'Armures : consommer de l'Acier à intervalle décroissant avec le niveau,
/// produire une unité, avec une chance de doublement (<c>SMITH_DOUBLE_PROD_CHANCE_PERCENT</c>).
///
/// <para>Les deux méthodes se ressemblent beaucoup mais restent écrites séparément : les factoriser
/// demandait un délégué pour l'intervalle et un aiguillage de type pour <c>LastProductionTick</c>,
/// déclaré sur chaque forge et non sur une base commune — plus d'indirection que de duplication
/// supprimée.</para>
///
/// <para>Elles restent aussi deux étapes distinctes du tick (voir <c>HarvestController</c>) : leur
/// ordre relatif fixe l'ordre de consommation du PRNG, donc le déterminisme de la partie.</para>
/// </summary>
internal sealed class SmithProductionEngine
{
    private WorldState? _state;
    private GamePRNG? _prng;

    internal void Initialize(WorldState? state, GamePRNG? prng)
    {
        _state = state;
        _prng = prng;
    }

    internal void TickWeaponSmiths(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            var cities = civ.GetCitiesWith(BuildingType.WeaponSmith);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var smith = city.FindBuilding<WeaponSmith>(BuildingType.WeaponSmith);
                if (smith == null || smith.Level < 1 || smith.ActivationStatus != ActivationStatus.ACTIVE) continue;

                long cooldown = HarvestController.GetWeaponSmithInterval(smith.Level);
                // coldStartOnZero: true — une Forge d'Armes tout juste construite/promue en cours de
                // partie déjà avancée ne doit pas rattraper tout l'écoulé depuis le tick 0 (voir
                // SoldierProductionEngine.ProduceSoldiers).
                long lastTick = smith.LastProductionTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, cooldown, coldStartOnZero: true);
                smith.LastProductionTick = lastTick;
                if (cycles <= 0) continue;

                // Rejoué cycle par cycle : le stock d'Acier peut s'épuiser en cours de route, et le
                // doublement est un tirage indépendant par cycle.
                for (long c = 0; c < cycles; c++)
                {
                    if (civ.GetResourceQuantity(Resource.SteelWeapon) >= civ.GetResourceMaxQuantity(Resource.SteelWeapon)) break;

                    if (civ.GetResourceQuantity(Resource.Steel) < WeaponSmith.SteelInputPerWeapon)
                    {
                        civ.RaiseLowStock(Resource.Steel);
                        break;
                    }

                    civ.RemoveResource(Resource.Steel, WeaponSmith.SteelInputPerWeapon);
                    civ.AddResource(Resource.SteelWeapon, 1);
                    if (_prng!.Next(100) < civ.SmithDoubleProdChancePercent)
                        civ.AddResource(Resource.SteelWeapon, 1);
                }
            }
        }
    }

    internal void TickArmorSmiths(long currentTick)
    {
        if (_state == null) return;

        foreach (var civ in _state.Civilizations)
        {
            var cities = civ.GetCitiesWith(BuildingType.ArmorSmith);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                var smith = city.FindBuilding<ArmorSmith>(BuildingType.ArmorSmith);
                if (smith == null || smith.Level < 1 || smith.ActivationStatus != ActivationStatus.ACTIVE) continue;

                long cooldown = HarvestController.GetArmorSmithInterval(smith.Level);
                // coldStartOnZero: true — même garde-fou que TickWeaponSmiths ci-dessus.
                long lastTick = smith.LastProductionTick;
                long cycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref lastTick, cooldown, coldStartOnZero: true);
                smith.LastProductionTick = lastTick;
                if (cycles <= 0) continue;

                // Rejoué cycle par cycle : le stock d'Acier peut s'épuiser en cours de route, et le
                // doublement est un tirage indépendant par cycle.
                for (long c = 0; c < cycles; c++)
                {
                    if (civ.GetResourceQuantity(Resource.SteelArmor) >= civ.GetResourceMaxQuantity(Resource.SteelArmor)) break;

                    if (civ.GetResourceQuantity(Resource.Steel) < ArmorSmith.SteelInputPerArmor)
                    {
                        civ.RaiseLowStock(Resource.Steel);
                        break;
                    }

                    civ.RemoveResource(Resource.Steel, ArmorSmith.SteelInputPerArmor);
                    civ.AddResource(Resource.SteelArmor, 1);
                    if (_prng!.Next(100) < civ.SmithDoubleProdChancePercent)
                        civ.AddResource(Resource.SteelArmor, 1);
                }
            }
        }
    }
}
