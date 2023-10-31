using AzuAutoStore.Util;
using HarmonyLib;

namespace AzuAutoStore.Patches;

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
static class ItemDropStartPatch
{
    static void Postfix(ItemDrop __instance)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug("ItemDrop Start");
        foreach (Container container in Boxes.Containers)
        {
            if (ItemDrop.s_instances.Count == ContainerAwakePatch.lastCount) continue;
            ContainerAwakePatch.ItemDroppedNearby(container);
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"ItemDrop s_instances count changed from {ContainerAwakePatch.lastCount} to {ItemDrop.s_instances.Count}");
            ContainerAwakePatch.lastCount = ItemDrop.s_instances.Count;
        }
    }
}