using AzuAutoStore.Util;
using HarmonyLib;
using UnityEngine;

namespace AzuAutoStore.Patches;

[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
static class ItemDropStartPatch
{
    static void Postfix(ItemDrop __instance)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug("ItemDrop Start");
        for (int index = 0; index < Boxes.Containers.Count; ++index)
        {
            Container container = Boxes.Containers[index];
            if (ItemDrop.s_instances.Count == ContainerAwakePatch.lastCount) continue;
            float distance = Vector3.Distance(container.transform.position, __instance.transform.position);
            if (!(distance <= Functions.GetContainerRange(container))) continue;
            ContainerAwakePatch.ItemDroppedNearby(container);
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"ItemDrop s_instances count changed from {ContainerAwakePatch.lastCount} to {ItemDrop.s_instances.Count}");
            ContainerAwakePatch.lastCount = ItemDrop.s_instances.Count;
        }
    }
}