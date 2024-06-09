using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.APIs;
using AzuAutoStore.Patches.Favoriting;
using AzuAutoStore.Util;
using UnityEngine;

namespace AzuAutoStore.Interfaces;

public class kgDrawer(ItemDrawers_API.Drawer _drawer) : IContainer
{
    private static bool CantStoreFavorite(ItemDrop.ItemData item, UserConfig playerConfig)
    {
        return playerConfig.IsItemNameOrSlotFavorited(item);
    }

    internal static void LogDebug(string data)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug(data);
    }

    public bool ContainsItem(string prefab, int amount, out int result)
    {
        result = 0;
        if (_drawer.Prefab != prefab) return false;
        result = _drawer.Amount;
        return result >= amount;
    }

    public void RemoveItem(string prefab, int amount)
    {
        amount = Mathf.Min(amount, _drawer.Amount);
        _drawer.Remove(amount);
    }

    public int TryStore()
    {
        if (Player.m_localPlayer == null) return 0;

        int total = 0;
        List<ItemDrop.ItemData>? items = Player.m_localPlayer.GetInventory().GetAllItems();
        for (int j = items.Count - 1; j >= 0; j--)
        {
            ItemDrop.ItemData? item = items[j];

            if (item.m_dropPrefab.name != _drawer.Prefab) continue;


            if (item.m_equipped)
            {
                LogDebug($"Skipping equipped item {item.m_dropPrefab.name}");
                continue;
            }

            // If the item.m_gridPos.x is 1-8 and item.m_gridPos.y is 0 (the first row), then do not store the item if _playerIgnoreHotbar is true
            if (item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0 &&
                AzuAutoStorePlugin.PlayerIgnoreHotbar.Value == AzuAutoStorePlugin.Toggle.On)
            {
                LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in the hotbar");
                continue;
            }

            if (AzuExtendedPlayerInventory.API.IsLoaded())
            {
                // Get quick slot positions
                List<ItemDrop.ItemData> quickSlotsItems = AzuExtendedPlayerInventory.API.GetQuickSlotsItems();


                // Check if the item is in the quick slots
                if (quickSlotsItems.Any(quickSlotItem => quickSlotItem.m_gridPos == item.m_gridPos))
                {
                    LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in your quick slots");
                    continue;
                }
            }

            if (CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID())))
            {
                LogDebug($"Skipping favorited item/slot {item.m_dropPrefab.name}");
                continue;
            }

            int stack = item.m_stack;
            total += stack;
            Player.m_localPlayer.GetInventory().RemoveItem(item);
            _drawer.Add(stack);
            LogDebug($"Stored {stack} {item.m_dropPrefab.name} into {_drawer.gameObject.name}");
            if (Boxes.ContainersToPing.Contains(this)) continue;
            Boxes.ContainersToPing.Add(this);
        }

        return total;
    }

    public int TryStoreThisItem(ItemDrop.ItemData? singleItemData = null, Inventory? playerInventory = null)
    {
        if (Player.m_localPlayer == null) return 0;
        Inventory? inventory = playerInventory ?? Player.m_localPlayer.GetInventory();
        int total = 0;
        List<ItemDrop.ItemData>? items = singleItemData == null ? inventory.GetAllItems() : [singleItemData];
        for (int j = items.Count - 1; j >= 0; j--)
        {
            ItemDrop.ItemData? item = items[j];
            if (item.m_equipped)
            {
                LogDebug($"Skipping equipped item {item.m_dropPrefab.name}");
                continue;
            }

            // If the item.m_gridPos.x is 1-8 and item.m_gridPos.y is 0 (the first row), then do not store the item if _playerIgnoreHotbar is true
            if (item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0 && AzuAutoStorePlugin.PlayerIgnoreHotbar.Value == AzuAutoStorePlugin.Toggle.On)
            {
                LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in the hotbar");
                continue;
            }

            if (AzuExtendedPlayerInventory.API.IsLoaded())
            {
                // Get quick slot positions
                List<ItemDrop.ItemData> quickSlotsItems = AzuExtendedPlayerInventory.API.GetQuickSlotsItems();


                // Check if the item is in the quick slots
                if (quickSlotsItems.Any(quickSlotItem => quickSlotItem.m_gridPos == item.m_gridPos))
                {
                    LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in your quick slots");
                    continue;
                }
            }

            if (CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID())))
            {
                LogDebug($"Skipping favorited item/slot {item.m_dropPrefab.name}");
                continue;
            }

            LogDebug($"Checking item {item.m_dropPrefab.name}");
            if (!TryStoreToKG(_drawer, ref item, true, singleItemData != null)) continue;
            total += item.m_stack;
            inventory.RemoveItem(item);
            LogDebug($"Stored {item.m_stack} {item.m_dropPrefab.name} into {_drawer.gameObject.name}");
            if (Boxes.ContainersToPing.Contains(this)) continue;
            Boxes.ContainersToPing.Add(this);
        }

        return total;
    }

    internal bool TryStoreToKG(ItemDrawers_API.Drawer nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false, bool singleItemData = false)
    {
        bool changed = false;
        LogDebug($"Checking container {_drawer.gameObject.name}");
        if (!MiscFunctions.CheckItemSharedIntegrity(item)) return changed;
        if (AzuAutoStorePlugin.MustHaveExistingItemToPull.Value == AzuAutoStorePlugin.Toggle.On && nearbyContainer.Prefab != item.m_dropPrefab.name)
        {
            if (singleItemData)
            {
                LogDebug($"Skipping {item.m_dropPrefab.name} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} is not in nearby containers</color>");
            }

            return false;
        }

        if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(_drawer.gameObject.name), item.m_dropPrefab.name)) return false;

        LogDebug($"Auto storing {item.m_dropPrefab.name} in {_drawer.gameObject.name}");
        while (item.m_stack > 1 && nearbyContainer.Prefab == item.m_dropPrefab.name)
        {
            changed = true;
            item.m_stack--;
            ItemDrop.ItemData newItem = item.Clone();
            newItem.m_stack = 1;
            _drawer.Add(newItem.m_stack);
        }

        if (item.m_stack == 1 && nearbyContainer.Prefab == item.m_dropPrefab.name)
        {
            ItemDrop.ItemData newItem = item.Clone();
            item.m_stack = 0;
            _drawer.Add(newItem.m_stack);
            changed = true;
        }

        if (!changed) return changed;
        if (!fromPlayer)
        {
            if (!Boxes.ContainersToPing.Contains(this))
                Boxes.ContainersToPing.Add(this);
        }

        return changed;
    }

    public GameObject gameObject => _drawer.gameObject;
    public ZNetView m_nview => _drawer.m_nview;

    public bool IsOwner() => true;

    public static kgDrawer Create(ItemDrawers_API.Drawer drawer) => new(drawer);
}