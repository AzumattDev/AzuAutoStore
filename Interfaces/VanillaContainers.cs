﻿using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.Patches.Favoriting;
using AzuAutoStore.Util;
using UnityEngine;

namespace AzuAutoStore.Interfaces;

public class VanillaContainers(Container _container) : IContainer
{
    public GameObject gameObject => _container.gameObject;
    public ZNetView m_nview => _container.m_nview;

    public static bool CantStoreFavorite(ItemDrop.ItemData item, UserConfig playerConfig)
    {
        return playerConfig.IsItemNameOrSlotFavorited(item);
    }

    internal static void LogDebug(string data)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug(data);
    }

    public int TryStore()
    {
        if (Player.m_localPlayer == null) return 0;

        int total = 0;
        List<ItemDrop.ItemData>? items = Player.m_localPlayer.GetInventory().GetAllItems();
        for (int j = items.Count - 1; j >= 0; --j)
        {
            ItemDrop.ItemData? item = items[j];
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

            LogDebug($"Checking item {item.m_dropPrefab.name}");
            int originalAmount = item.m_stack;
            if (!TryStore(_container, ref item, true)) continue;
            if (item.m_stack >= originalAmount) continue;
            total += originalAmount - item.m_stack;
            Player.m_localPlayer.GetInventory().RemoveItem(item, originalAmount - item.m_stack);
            LogDebug($"Stored {originalAmount - item.m_stack} {item.m_dropPrefab.name} into {_container.name}");
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
        //List<ItemDrop.ItemData>? items = singleItemData == null ? inventory.GetAllItems() : [singleItemData];
        List<ItemDrop.ItemData>? items = singleItemData == null ? null : [singleItemData];
        if (items == null)
        {
            return 0;
        }

        if (items.Count == 0)
        {
            return 0;
        }

        for (int j = items.Count - 1; j >= 0; --j)
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
            int originalAmount = item.m_stack;
            if (!Functions.TryStore(_container, ref item, true, singleItemData != null)) continue;
            if (item.m_stack >= originalAmount) continue;
            total += originalAmount - item.m_stack;
            inventory.RemoveItem(item, originalAmount - item.m_stack);
            LogDebug($"Stored {originalAmount - item.m_stack} {item.m_dropPrefab.name} into {_container.name}");
            if (Boxes.ContainersToPing.Contains(this)) continue;
            Boxes.ContainersToPing.Add(this);
        }

        return total;
    }

    internal bool TryStore(Container nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false)
    {
        bool changed = false;
        LogDebug($"Checking container {nearbyContainer.name}");
        if (!MiscFunctions.CheckItemSharedIntegrity(item)) return changed;
        if (AzuAutoStorePlugin.MustHaveExistingItemToPull.Value == AzuAutoStorePlugin.Toggle.On && !nearbyContainer.GetInventory().HaveItem(item.m_shared.m_name))
            return false;
        if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(nearbyContainer.transform.root.name), item.m_dropPrefab.name)) return false;

        LogDebug($"Auto storing {item.m_dropPrefab.name} in {nearbyContainer.name}");
        while (item.m_stack > 1 && nearbyContainer.GetInventory().CanAddItem(item, 1))
        {
            changed = true;
            item.m_stack--;
            ItemDrop.ItemData newItem = item.Clone();
            newItem.m_stack = 1;
            nearbyContainer.GetInventory().AddItem(newItem);
        }

        if (item.m_stack == 1 && nearbyContainer.GetInventory().CanAddItem(item, 1))
        {
            ItemDrop.ItemData newItem = item.Clone();
            item.m_stack = 0;
            nearbyContainer.GetInventory().AddItem(newItem);
            changed = true;
        }

        if (!changed) return changed;
        if (!fromPlayer)
        {
            Functions.PingContainer(_container.gameObject);
        }

        nearbyContainer.Save();

        return changed;
    }

    public bool IsOwner() => _container.m_nview.IsOwner();

    public Inventory GetInventory()
    {
        return _container.GetInventory();
    }

    public static VanillaContainers Create(Container container) => new(container);
}