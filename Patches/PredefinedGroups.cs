using AzuAutoStore.Util;
using HarmonyLib;

namespace AzuAutoStore.Patches;

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
static class PredefinedGroupGrab
{
    static void Postfix(ObjectDB __instance)
    {
        if (!ZNetScene.instance)
            return;
        MiscFunctions.CreatePredefinedGroups(__instance);
    }
}