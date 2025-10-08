using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class TritiumFireReaction : IGasReactionEffect
    {
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;
            var initialTrit = mixture.GetMoles(Gas.Tritium);
            var initialOxy = mixture.GetMoles(Gas.Oxygen);

            var oxyRatio = initialOxy / (initialTrit * Atmospherics.HydrogenFuelOxyRatio);
            // Reaction rate asymptotically approaches its maximum as oxygen concentration increases, reaching half its maximum at the ideal ratio.
            var reactionRate = Math.Min(initialTrit, initialOxy / Atmospherics.HydrogenFuelOxyRatio) * (1f - 1f / (1f + oxyRatio)) / Atmospherics.HydrogenBurnRate;

            if (reactionRate > 0)
            {
                energyReleased += Atmospherics.FireHydrogenEnergyReleased * reactionRate;

                // TODO ATMOS Radiation pulse here!

                // Conservation of mass is important.
                mixture.AdjustMoles(Gas.WaterVapor, reactionRate);
                mixture.AdjustMoles(Gas.Tritium, -reactionRate);
                mixture.AdjustMoles(Gas.Oxygen, -reactionRate / Atmospherics.HydrogenFuelOxyRatio);

                mixture.ReactionResults[(byte)GasReaction.Fire] += reactionRate;
            }

            energyReleased /= heatScale; // adjust energy to make sure speedup doesn't cause mega temperature rise
            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = ((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity);
            }

            if (location != null)
            {
                temperature = mixture.Temperature;
                if (temperature > Atmospherics.FireMinimumTemperatureToExist)
                {
                    atmosphereSystem.HotspotExpose(location, temperature, mixture.Volume);
                }
            }

            return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
        }
    }
}
