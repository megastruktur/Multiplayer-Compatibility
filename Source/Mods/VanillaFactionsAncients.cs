﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>Vanilla Factions Expanded - Ancients</summary>
    /// <see href="https://github.com/AndroidQuazar/VanillaFactionsExpandedAncients"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=2654846754"/>
    [MpCompatFor("VanillaExpanded.VFEA")]
    internal class VanillaFactionsAncients
    {
        private delegate void CallOnChosen(Window dialog, Def power, Def weakness);

        private static AccessTools.FieldRef<object, ThingComp> operationPodField;
        private static CallOnChosen onChosen;
        private static Type choosePowerDialogType;

        public VanillaFactionsAncients(ModContentPack mod)
        {
            // Supply slingshot launch gizmo (after 2 possible confirmation)
            MP.RegisterSyncMethod(AccessTools.TypeByName("VFEAncients.CompSupplySlingshot"), "TryLaunch");

            // VFEAncients.CompGeneTailoringPod:StartOperation requires SyncWorker for Operation
            // (Method inside of LatePatch)
            var type = AccessTools.TypeByName("VFEAncients.Operation");
            operationPodField = AccessTools.FieldRefAccess<ThingComp>(type, "Pod");
            MP.RegisterSyncWorker<object>(SyncOperation, type, true);

            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }

        public static void LatePatch()
        {
            // Ancient PD turret - toggle aiming at drop pods, enemies, explosive projectiles
            MpCompat.RegisterLambdaMethod("VFEAncients.Building_TurretPD", "GetGizmos", 1, 3, 5);

            var type = AccessTools.TypeByName("VFEAncients.CompGeneTailoringPod");
            // Start gene tailoring operation (after danger warning confirmation)
            MP.RegisterSyncMethod(type, "StartOperation");
            // Cancel operation (before starting it)
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 8);

            // (Dev) instant success/failure 
            MpCompat.RegisterLambdaMethod(type, "CompGetGizmosExtra", 9, 10).SetDebugOnly();
            // (Dev) instant finish, random result not synced, as it calls CompleteOperation
            // would cause a tiny conflict, not worth bothering with it
            // (I think it would need to be done without SetDebugOnly, or it would cause issues)

            choosePowerDialogType = AccessTools.TypeByName("VFEAncients.Dialog_ChoosePowers");
            var powerDefType = AccessTools.TypeByName("VFEAncients.PowerDef");
            var tupleType = typeof(Tuple<,>).MakeGenericType(powerDefType, powerDefType);
            onChosen = CompileCallOnChosen(powerDefType, tupleType);
            MP.RegisterSyncMethod(typeof(VanillaFactionsAncients), nameof(SyncedChoosePower));
            MP.RegisterSyncWorker<Window>(SyncDialogChoosePower, choosePowerDialogType);
            DialogUtilities.RegisterPauseLock(choosePowerDialogType);
            MpCompat.harmony.Patch(AccessTools.Method(choosePowerDialogType, nameof(Window.DoWindowContents)),
                transpiler: new HarmonyMethod(typeof(VanillaFactionsAncients), nameof(ReplaceButtons)));
        }

        #region SyncWorkers

        private static void SyncOperation(SyncWorker sync, ref object operation)
        {
            if (sync.isWriting)
            {
                sync.Write(operationPodField(operation));
                // Right now we have 2 types it could be, but there could be more in the future
                sync.Write(operation.GetType());
            }
            else
            {
                var pod = sync.Read<ThingComp>();
                var type = sync.Read<Type>();

                // All the current types right now have 1 argument
                operation = Activator.CreateInstance(type, pod);
            }
        }

        private static void SyncDialogChoosePower(SyncWorker sync, ref Window window)
        {
            if (!sync.isWriting)
                window = Find.WindowStack.windows.FirstOrDefault(x => x.GetType() == choosePowerDialogType);
        }

        #endregion

        #region Dialog

        private static bool ChoosePower(Rect rect, string text, bool drawBackground, bool doMouseoverSound, bool active, Def power, Def weakness)
        {
            var buttonResult = Widgets.ButtonText(rect, text, drawBackground, doMouseoverSound, active);

            if (!MP.IsInMultiplayer || !buttonResult)
                return buttonResult;

            SyncedChoosePower(power, weakness);
            return false;
        }

        private static void SyncedChoosePower(Def power, Def weakness)
        {
            var dialog = Find.WindowStack.windows.FirstOrDefault(x => x.GetType() == choosePowerDialogType);
            if (dialog == null) return;

            onChosen(dialog, power, weakness);
            dialog.Close();
        }

        private static IEnumerable<CodeInstruction> ReplaceButtons(IEnumerable<CodeInstruction> instr)
        {
            var target = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText), new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool) });
            var replacement = AccessTools.Method(typeof(VanillaFactionsAncients), nameof(ChoosePower));

            foreach (var ci in instr)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo method && method == target)
                {
                    ci.operand = replacement;

                    yield return new CodeInstruction(OpCodes.Ldloc_1); // Power
                    yield return new CodeInstruction(OpCodes.Ldloc_2); // Weakness
                }

                yield return ci;
            }
        }

        private static CallOnChosen CompileCallOnChosen(Type powerDefType, Type tupleType)
        {
            // Setup the parameters to the delegate
            var dialogParam = Expression.Parameter(typeof(Window), "dialog");
            var powerParam = Expression.Parameter(typeof(Def), "power");
            var weaknessParam = Expression.Parameter(typeof(Def), "weakness");

            // We'll need to cast from Window to Dialog_ChoosePower and Def to PowerDef to be able to construct our Tuple<PowerDef,PowerDef>
            var dialogCast = Expression.Convert(dialogParam, choosePowerDialogType);
            var powerCast = Expression.Convert(powerParam, powerDefType);
            var weaknessCast = Expression.Convert(weaknessParam, powerDefType);

            // Create the tuple from our parameters after casting them
            var newTuple = Expression.New(AccessTools.Constructor(tupleType, new[] { powerDefType, powerDefType }), powerCast, weaknessCast);

            // Get the field onChosen of type Action<PowerDef,PowerDef> from Dialog_ChoosePower
            var onChosenField = Expression.Field(dialogCast, AccessTools.Field(choosePowerDialogType, "onChosen"));
            // Invoke the action with the tuple as our parameter
            var invoke = Expression.Invoke(onChosenField, newTuple);

            // Create the expression lambda that'll do all our operations
            var lambda = Expression.Lambda(typeof(CallOnChosen), invoke, dialogParam, powerParam, weaknessParam);
            // Compile and return the lambda
            return (CallOnChosen)lambda.Compile();
        }

        #endregion
    }
}