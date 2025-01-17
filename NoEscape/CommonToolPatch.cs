﻿using BaseX;
using FrooxEngine;
using HarmonyLib;
using System.Runtime.CompilerServices;

namespace NoEscape
{
    [HarmonyPatch(typeof(CommonTool))]
    internal static class CommonToolPatches
    {
        private static readonly ConditionalWeakTable<CommonTool, ToolData> toolDataTable = new ConditionalWeakTable<CommonTool, ToolData>();

        [HarmonyPrefix]
        [HarmonyPatch("HoldMenu")]
        private static void HoldMenuPrefix(CommonTool __instance, ref float ___panicCharge)
        {
            NoEscape.PollRespawnCloudVariable(__instance);
            var toolData = toolDataTable.GetOrCreateValue(__instance);

            var panicCharge = toolData.PanicCharge;
            toolData.PanicCharge += __instance.Time.Delta;
            // Allow changing to disconnect instead of respawn only until the regular 2s are over.
            toolData.IsDisconnect = !(__instance.Inputs.Grab.Held || __instance.OtherTool.Inputs.Grab.Held);

            if (!isRespawnGesture(__instance) || NoEscape.RespawnAllowed || (NoEscape.DisconnectAllowed && toolData.IsDisconnect))
            {
                if (toolData.PanicCharge >= 2)
                    ___panicCharge = toolData.PanicCharge;

                return;
            }

            // Let it activate when held long enough
            if (NoEscape.PanicChargeOverride > 0 && toolData.PanicCharge >= NoEscape.PanicChargeOverride)
                ___panicCharge = panicCharge;
            // Disable notification of it not going to work
            else if (NoEscape.PanicChargeNotification < 0)
                ___panicCharge = MathX.Min(1.9f - __instance.Time.Delta, panicCharge);
            // Stop the vibration to signal it won't work when held long enough
            else if (NoEscape.PanicChargeNotification >= 0 && toolData.PanicCharge >= NoEscape.PanicChargeNotification)
                ___panicCharge = 0;

            if (___panicCharge >= 2)
                toolData.PanicCharge = float.MinValue;
        }

        private static bool isRespawnGesture(CommonTool tool)
        {
            return tool.World == Userspace.UserspaceWorld
                && tool.IsNearHead && tool.OtherTool != null && tool.Side.Value == Chirality.Left
                && tool.OtherTool.Inputs.Menu.Held && tool.OtherTool.IsNearHead;
        }

        [HarmonyPostfix]
        [HarmonyPatch("StartMenu")]
        private static void StartMenuPostfix(CommonTool __instance)
        {
            toolDataTable.GetOrCreateValue(__instance).PanicCharge = 0;
        }

        private class ToolData
        {
            private bool isDisconnect;

            public bool IsDisconnect
            {
                get => isDisconnect;
                set => isDisconnect = PanicCharge < 2 ? value : isDisconnect;
            }

            public float PanicCharge { get; set; } = 0;
        }
    }
}