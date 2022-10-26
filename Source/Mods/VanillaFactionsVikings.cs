using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Vikings by Oskar Potocki, Erin, Sarg Bjornson, erdelf, Kikohi, Taranchuk, Helixien, Chowder</summary>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2231295285"/>
    /// <see href="https://github.com/AndroidQuazar/Vanilla-Factions-Expanded-Vikings"/>
    [MpCompatFor("OskarPotocki.VFE.Vikings")]
    class VanillaFactionsVikings
    {

        public VanillaFactionsVikings(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
            
            // Debug stuff
            var type = AccessTools.TypeByName("VFEV.Apiary");
            MpCompat.RegisterLambdaMethod(type, "GetGizmos", 0, 1).SetDebugOnly();

            // This method seems unused... But I guess it's better to be safe than sorry.
            PatchingUtilities.PatchSystemRand(AccessTools.Method(type, "ResetTend"), false);
        }

        public void LatePatch() {}
    }
}
