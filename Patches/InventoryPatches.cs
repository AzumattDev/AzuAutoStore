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


        if (AzuAutoStorePlugin.SingleItemShortcut.Value.IsDown())
        {
            ItemDrop.ItemData? item = __instance.m_inventory.GetItemAt(element.m_pos.x, element.m_pos.y);
            if (item == null) return;
            Functions.TryStoreThisItem(item, __instance.m_inventory);
        }
    }
}