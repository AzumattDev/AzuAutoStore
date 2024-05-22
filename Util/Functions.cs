using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.Patches.Favoriting;
using BepInEx.Configuration;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AzuAutoStore.Util;

public class Functions
{
    internal static void TextAreaDrawer(ConfigEntryBase entry)
    {
        GUILayout.ExpandHeight(true);
        GUILayout.ExpandWidth(true);
        entry.BoxedValue = GUILayout.TextArea((string)entry.BoxedValue, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    }

    internal static float GetContainerRange(Container container)
    {
        if (AzuAutoStorePlugin.yamlData == null)
        {
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogError("yamlData is null when trying to get the container range for a container. Make sure that your YAML file is not empty or to call DeserializeYamlFile() before using GetContainerRange.");
            return -1f;
        }

        if (container.GetInventory() == null)
            return -1f;

        // Try to get container settings from YAML configuration
        string containerName = MiscFunctions.GetPrefabName(container.transform.root.name);
        if (AzuAutoStorePlugin.yamlData.TryGetValue(containerName, out object containerData))
        {
            if (containerData is Dictionary<object, object> containerInfo)
            {
                if (containerInfo.TryGetValue("range", out object rangeObj) && float.TryParse(rangeObj.ToString(), out float range))
                {
                    return range;
                }
            }
            else
            {
                AzuAutoStorePlugin.AzuAutoStoreLogger.LogError($"Unable to cast containerData for container '{containerName}' to Dictionary<object, object>.");
                return -1f;
            }
        }


        return AzuAutoStorePlugin.FallbackRange.Value;
    }

    private static bool CantStoreFavorite(ItemDrop.ItemData item, UserConfig playerConfig)
    {
        return playerConfig.IsItemNameOrSlotFavorited(item);
    }

    internal static bool TryStore(Container nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false, bool singleItemData = false)
    {
        bool changed = false;
        LogDebug($"Checking container {nearbyContainer.name}");
        if (!MiscFunctions.CheckItemSharedIntegrity(item)) return changed;
        if (AzuAutoStorePlugin.MustHaveExistingItemToPull.Value == AzuAutoStorePlugin.Toggle.On && !nearbyContainer.GetInventory().HaveItem(item.m_shared.m_name))
        {
            if (singleItemData)
            {
                LogDebug($"Skipping {item.m_dropPrefab.name} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} is not in nearby containers</color>");
            }

            return false;
        }

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
            PingContainer(nearbyContainer);
        }

        nearbyContainer.Save();

        return changed;
    }

    internal static int TryStoreInContainer(Container nearbyContainer, ItemDrop.ItemData? singleItemData = null, Inventory? playerInventory = null)
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
            int originalAmount = item.m_stack;
            if (!TryStore(nearbyContainer, ref item, true, singleItemData != null)) continue;
            if (item.m_stack >= originalAmount) continue;
            total += originalAmount - item.m_stack;
            inventory.RemoveItem(item, originalAmount - item.m_stack);
            LogDebug($"Stored {originalAmount - item.m_stack} {item.m_dropPrefab.name} into {nearbyContainer.name}");
            if (Boxes.ContainersToPing.Contains(nearbyContainer)) continue;
            Boxes.ContainersToPing.Add(nearbyContainer);
        }

        return total;
    }

    internal static int InProgressStores = 0;
    internal static int InProgressTotal = 0;

    internal static void TryStore()
    {
        if (Player.m_localPlayer == null) return;
        LogDebug("Trying to store items from player inventory");
        // Check all items in the player inventory where the items are not equipped

        Container?[] uncheckedContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuAutoStorePlugin.PlayerRange.Value).ToArray();

        int total = 0;
        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not { } nearbyContainer || !nearbyContainer.m_nview.IsOwner())
            {
                continue;
            }

            uncheckedContainers[i] = null;
            total += TryStoreInContainer(nearbyContainer, null, null);
        }

        if (InProgressStores > 0)
        {
            LogDebug($"Found {InProgressStores} requests for container ownership still pending...");
            InProgressTotal += total;
            return;
        }

        InProgressStores = uncheckedContainers.Count(c => c is not null);
        if (InProgressStores > 0)
        {
            InProgressTotal = total;

            IEnumerator End()
            {
                yield return new WaitForSeconds(1);
                StoreSuccess(InProgressTotal);
                InProgressStores = 0;
            }

            AzuAutoStorePlugin.self.StartCoroutine(End());

            foreach (Container? nearbyContainer in uncheckedContainers)
            {
                // prevent claiming ownership of other players (e.g. through adventure backpacks)
                Player? player = nearbyContainer?.m_nview.GetComponent<Player>();

                if (!player || player == Player.m_localPlayer)
                {
                    nearbyContainer?.m_nview.InvokeRPC("Autostore Ownership");
                }
            }
        }
        else
        {
            StoreSuccess(total);
        }
    }

    internal static void TryStoreThisItem(ItemDrop.ItemData itemData, Inventory m_inventory)
    {
        if (Player.m_localPlayer == null) return;
        LogDebug($"Trying to store {itemData.m_shared.m_name} from player inventory");
        // Check all items in the player inventory where the items are not equipped

        Container?[] uncheckedContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuAutoStorePlugin.PlayerRange.Value).ToArray();

        int total = 0;
        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not { } nearbyContainer || !nearbyContainer.m_nview.IsOwner())
            {
                continue;
            }

            uncheckedContainers[i] = null;
            total += TryStoreInContainer(nearbyContainer, itemData, m_inventory);
        }

        if (InProgressStores > 0)
        {
            LogDebug($"Found {InProgressStores} requests for container ownership still pending...");
            InProgressTotal += total;
            return;
        }

        InProgressStores = uncheckedContainers.Count(c => c is not null);
        if (InProgressStores > 0)
        {
            InProgressTotal = total;

            IEnumerator End()
            {
                yield return new WaitForSeconds(1);
                StoreSuccess(InProgressTotal);
                InProgressStores = 0;
            }

            AzuAutoStorePlugin.self.StartCoroutine(End());

            foreach (Container? nearbyContainer in uncheckedContainers)
            {
                // prevent claiming ownership of other players (e.g. through adventure backpacks)
                Player? player = nearbyContainer?.m_nview.GetComponent<Player>();

                if (!player || player == Player.m_localPlayer)
                {
                    nearbyContainer?.m_nview.InvokeRPC("Autostore Ownership");
                }
            }
        }
        else
        {
            StoreSuccess(total);
        }
    }

    internal static void StoreSuccess(int total)
    {
        if (total > 0)
        {
            InProgressTotal = 0;
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Stored {total} items from your inventory into nearby containers");
            foreach (Container c in Boxes.ContainersToPing)
            {
                PingContainer(c);
            }

            Boxes.ContainersToPing.Clear();
        }
    }


    internal static void PingContainer(Component container)
    {
        if (AzuAutoStorePlugin.PingContainers.Value == AzuAutoStorePlugin.Toggle.On && container.gameObject.GetComponent<ChestPingEffect>() == null)
            container.gameObject.AddComponent<ChestPingEffect>();

        if (AzuAutoStorePlugin.HighlightContainers.Value == AzuAutoStorePlugin.Toggle.On && container.gameObject.GetComponent<HighLightChest>() == null)
            container.gameObject.AddComponent<HighLightChest>();
    }


    internal static void LogDebug(string data)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug(data);
    }

    internal static void LogError(string data)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogError(data);
    }

    internal static void LogInfo(string data)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogInfo(data);
    }

    internal static void LogWarning(string data)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogWarning(data);
    }
}