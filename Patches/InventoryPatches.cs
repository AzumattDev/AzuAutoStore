using System;
using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.Util;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

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
#if DEBUG
[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), [typeof(ItemDrop.ItemData)])]
static class InventoryAddItemPatch
{
    static void Postfix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
    {
        if (__instance == null || Player.m_localPlayer == null || Player.m_localPlayer.IsTeleporting() || __instance.Equals(Player.m_localPlayer.GetInventory()) || !Player.m_localPlayer.GetInventory().ContainsItem(item))
            return;
        if (InventoryGui.instance == null || !InventoryGui.instance.IsContainerOpen())
            return;
        if (!__result)
        {
            //if (TryAddToNearBy(item))
            if (Functions.TryStoreThisItem(item, __instance))
            {
                __result = true;
            }
        }
        else if (__instance.CountItems(item.m_shared.m_name) == item.m_stack && Functions.TryStoreThisItem(item, __instance))
        {
            __instance.RemoveItem(item);
        }
    }

// Can't use this method because it will cause item loss
// but my other method in Functions (when changed to return bool) works everywhere but here. Here, it causes hang ups and freezing.
    public static bool TryAddToNearBy(ItemDrop.ItemData item)
    {
        try
        {
            Container openedContainer = InventoryGui.instance.m_currentContainer;
            List<Container> list = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuAutoStorePlugin.PlayerRange.Value).Where<Container>((Func<Container, bool>)(c =>
            {
                if (openedContainer.GetHashCode() == c.GetHashCode() || openedContainer.GetHashCode() != c.GetHashCode())
                    return true;
                return c.m_nview.GetZDO().GetInt(ZDOVars.s_inUse, 0) == 0;
            })).ToList<Container>();
            foreach (Container targetContainer in list)
            {
                if (targetContainer.GetInventory().CanAddItem(item) && (openedContainer.GetHashCode() != targetContainer.GetHashCode()) && targetContainer.GetInventory().HaveItem(item.m_shared.m_name))
                {
                    Console.instance.Print(Localization.instance.Localize(item.m_shared.m_name) + " routed because target container has same item stack");
                    return AddItemToContainer(item, targetContainer);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogError("Failed to rearrange container items: " + ex?.ToString());
        }

        return false;
    }

    private static bool AddItemToContainer(ItemDrop.ItemData item, Container targetContainer)
    {
        int num = targetContainer.GetInventory().AddItem(item) ? 1 : 0;
        if (num == 0)
            return false;
        targetContainer.Save();
        targetContainer.GetInventory().Changed();
        if (!InventoryGui.instance.m_currentContainer.Equals(targetContainer))
            MessageHud.instance.QueueUnlockMsg(item.GetIcon(), item.m_shared.m_name, $"Redirected to {targetContainer.GetHoverName()}");
        Functions.PingContainer(targetContainer);
        return true;
    }
}
#endif
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