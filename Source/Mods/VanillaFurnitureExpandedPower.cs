using System;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Furniture Expanded - Power by Oskar Potocki and Sarg Bjornson</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477"/>
    /// <see href="https://github.com/AndroidQuazar/VanillaFurnitureExpanded-Power"/>
    /// Contribution to Multiplayer Compatibility by Sokyran and Reshiram
    [MpCompatFor("VanillaExpanded.VFEPower")]
    class VanillaPowerExpanded
    {

        public VanillaPowerExpanded(ModContentPack mod)
        {
            Type type;

            // Gizmos
            // Violence generator
            {
                type = AccessTools.TypeByName("VanillaPowerExpanded.CompSoulsPowerPlant");
                MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 1); // Toggle on/off
            }

            // RNG Fix
            {

                var methods = new[]
                {
                    "VanillaPowerExpanded.Building_SmallBattery:Tick",
                    "VanillaPowerExpanded.Building_SmallBattery:PostApplyDamage",
                    "VanillaPowerExpanded.WeatherEvent_CustomLightningStrike:FireEvent",
                    "VanillaPowerExpanded.MapComponentExtender:doMapSpawns",
                    "VanillaPowerExpanded.CompPlantHarmRadiusIfBroken:CompTick",
                    // HarmRandomPlantInRadius is only called by CompPlantHarmRadiusIfBroken:CompTick, no need for patching
                    // CompPowerPlantNuclear:AffectCell is only calling a seeded random
                    // PipeNetGrid pushes and pops all Rand calls, no need to patch
                    // CompPowerAdvancedWater:RebuildCache is only calling a seeded random
            };

                // These methods are loading resources in their .ctor, must be patched later
                var methodsForLater = new[]
                {
                    "VanillaPowerExpanded.CompPowerAdvancedWater:PostSpawnSetup",
                    "VanillaPowerExpanded.CompPowerAdvancedWind:PostSpawnSetup",
                };

                PatchingUtilities.PatchPushPopRand(methods);
                LongEventHandler.ExecuteWhenFinished(() => PatchingUtilities.PatchPushPopRand(methodsForLater));

            }
        }

        private static bool ReplaceHashOffsetTicks(ref int __result)
        {
            if (!MP.IsInMultiplayer) return true;

            // Look: ReplaceWindMapComponentTick
            // The same issue - using the default GetHashCode
            // The GasNet class (that this method uses hash from)
            // stores reference to the Map it's used on - maybe 
            // map ID could be used for calling HashOffset on?
            // I'm leaving it as is as it doesn't make a big
            // difference overall.
            __result = Find.TickManager.TicksGame;

            return false;
        }
    }
}
