using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.APIs;
using AzuAutoStore.Patches.Favoriting;
using UnityEngine;

namespace AzuAutoStore.Util;

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
    
    public GameObject gameObject => _drawer.gameObject;

    public bool IsOwner() => true;


    public static kgDrawer Create(ItemDrawers_API.Drawer drawer) => new(drawer);
}