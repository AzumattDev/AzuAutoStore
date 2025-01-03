using System.Linq;
using AzuAutoStore.Util;
using HarmonyLib;

namespace AzuAutoStore.Patches;

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
static class InventoryGridGetHoveredElementPatch
{
    static void Prefix(InventoryGrid __instance)
    {
        var flag = __instance.m_uiGroup.IsActive && ZInput.IsGamepadActive();
        InventoryGrid.Element element = flag
            ? __instance.GetElement(__instance.m_selected.x, __instance.m_selected.y, __instance.m_inventory.GetWidth())
            : __instance.GetHoveredElement();
        if (element == null) return;


        if (AzuAutoStorePlugin.SingleItemShortcut.Value.IsKeyDown())
        {
            ItemDrop.ItemData? item = __instance.m_inventory.GetItemAt(element.m_pos.x, element.m_pos.y);
            if (item == null) return;
            if (__instance.m_inventory == null) return;
            Functions.TryStoreThisItem(item, __instance.m_inventory);
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
static class PlayerUpdateTeleportPatch
{
    static void Prefix(Player __instance, float dt)
    {
        if (Player.m_localPlayer == null || !Player.m_localPlayer.m_teleporting)
            return;
        foreach (Container container in Boxes.Containers.ToList())
        {
            if (container == null || container.transform == null || container.GetInventory() == null)
            {
                if (!Boxes.Containers.Contains(container))
                    continue;
                Boxes.Containers.Remove(container);
            }
        }
    }
}