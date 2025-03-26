using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.Interfaces;
using AzuAutoStore.Patches;
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


    public static void LogContainerStatus(Container container)
    {
        LogIfBuildDebug($"Container {container.name} at {container.transform.position} is {(container.m_nview.IsOwner() ? "owned" : "not owned")} and has {(container.GetInventory()?.NrOfItems() ?? 0)} items.");
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

    internal static void CheckItemDropInstanceAndStore(ItemDrop itemDrop)
    {
        if (Boxes.Containers == null || itemDrop == null || itemDrop.transform == null)
        {
            return;
        }

        if (AzuAutoStorePlugin.ChestsPickupFromGround.Value == AzuAutoStorePlugin.Toggle.Off) return;
        if (itemDrop.m_nview == null || !itemDrop.m_nview.IsValid()) return;
        // Check if the itemdrop is a Fish and if it's not out of water before trying to store it.
        if (itemDrop.m_itemData.m_dropPrefab == null) return;
        if (itemDrop.m_itemData.m_dropPrefab.TryGetComponent<Fish>(out var fish))
        {
            if (!fish.IsOutOfWater())
                return;
        }

        for (int index = 0; index < Boxes.Containers.Count; ++index)
        {
            Container? container = Boxes.Containers[index];
            if (container == null || container.transform == null || container.GetInventory() == null)
            {
                continue;
            }

            float distance = Vector3.Distance(container.transform.position, itemDrop.transform.position);
            if (distance > Functions.GetContainerRange(container)) continue;
            // Check if storing is paused for this container
            bool isPaused = container.m_nview.GetZDO().GetBool(ContainerAwakePatch.storingPausedHash, false);
            if (isPaused)
                continue;
            if (!itemDrop.CanPickup(false))
            {
                itemDrop.RequestOwn();
            }
            else if (!itemDrop.m_nview!.HasOwner())
            {
                if (itemDrop.m_nview.m_zdo != null)
                {
                    try
                    {
                        itemDrop.m_nview.ClaimOwnership();
                    }
                    catch
                    {
                        // Not happy about this, but whatever. Fix later.
                    }
                }
            }

            if (!itemDrop.m_nview.IsOwner()) continue;
            Functions.LogDebug($"Nearby item name: {itemDrop.m_itemData.m_dropPrefab.name}");
            if (!Functions.TryStore(container, ref itemDrop.m_itemData))
                continue;
            itemDrop.Save();
            if (itemDrop.m_itemData.m_stack <= 0)
            {
                if (itemDrop.m_nview == null)
                    Object.DestroyImmediate(itemDrop.gameObject);
                else
                    ZNetScene.instance.Destroy(itemDrop.gameObject);
            }
        }
    }

    internal static bool TryStore(Container nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false, bool singleItemData = false)
    {
        bool changed = false;
        LogIfBuildDebug($"Checking container {nearbyContainer.name}");
        if (!MiscFunctions.CheckItemSharedIntegrity(item)) return changed;
        LogIfBuildDebug($"{item.m_dropPrefab.name}, Passed item integrity check");
        if (AzuAutoStorePlugin.MustHaveExistingItemToPull.Value == AzuAutoStorePlugin.Toggle.On && !nearbyContainer.GetInventory().HaveItem(item.m_shared.m_name))
        {
            if (singleItemData)
            {
                LogDebug($"Skipping {item.m_dropPrefab.name} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{item.m_dropPrefab.name}] is not in nearby containers</color>");
            }

            return false;
        }

        if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(nearbyContainer.transform.root.name), item.m_dropPrefab.name))
        {
            if (singleItemData)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{item.m_dropPrefab.name}] cannot be stored based on configuration settings</color>");
            }

            LogDebug($"{item.m_shared.m_name} cannot be stored based on configuration setting");
            return false;
        }

        if (!nearbyContainer.m_nview.IsOwner())
        {
            LogDebug($"Cannot store items in {nearbyContainer.name} because the player is not the owner.");
            return false;
        }

        if (!nearbyContainer.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID()))
        {
            LogDebug($"Cannot store items in {nearbyContainer.name} because the player does not have access.");
            return false;
        }

        while (item.m_stack > 1 && nearbyContainer.GetInventory().CanAddItem(item, 1))
        {
            changed = true;
            item.m_stack--;
            ItemDrop.ItemData newItem = item.Clone();
            newItem.m_stack = 1;
            LogDebug($"Auto storing {item.m_dropPrefab.name} in {nearbyContainer.name}");
            nearbyContainer.GetInventory().AddItem(newItem);
        }

        if (item.m_stack == 1 && nearbyContainer.GetInventory().CanAddItem(item, 1))
        {
            ItemDrop.ItemData newItem = item.Clone();
            item.m_stack = 0;
            LogDebug($"Auto storing {item.m_dropPrefab.name} in {nearbyContainer.name}");
            nearbyContainer.GetInventory().AddItem(newItem);
            changed = true;
        }

        if (!changed) return changed;
        if (!fromPlayer)
        {
            PingContainer(nearbyContainer.gameObject);
        }

        nearbyContainer.Save();

        return changed;
    }

    internal static int InProgressStores = 0;
    internal static int InProgressTotal = 0;

    internal static void TryStore()
    {
        if (Player.m_localPlayer == null) return;
        LogDebug("Trying to store items from player inventory");
        // Check all items in the player inventory where the items are not equipped

        IContainer?[] uncheckedContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuAutoStorePlugin.PlayerRange.Value).ToArray();

        int total = 0;
        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not { } nearbyContainer || !nearbyContainer.IsOwner())
            {
                continue;
            }

            uncheckedContainers[i] = null;
            total += nearbyContainer.TryStore();
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

            foreach (IContainer? nearbyContainer in uncheckedContainers)
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
        if (m_inventory != Player.m_localPlayer.GetInventory())
        {
            return;
        }

        LogDebug($"Trying to store {itemData.m_shared.m_name}");
        // Check all items in the player inventory where the items are not equipped

        IContainer?[] uncheckedContainers = Boxes.GetNearbyContainers(Player.m_localPlayer, AzuAutoStorePlugin.PlayerRange.Value).ToArray();

        int total = 0;
        for (int i = 0; i < uncheckedContainers.Length; ++i)
        {
            if (uncheckedContainers[i] is not { } nearbyContainer || !nearbyContainer.IsOwner())
            {
                continue;
            }

            uncheckedContainers[i] = null;
            total += nearbyContainer.TryStoreThisItem(itemData, m_inventory);
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

            foreach (IContainer? nearbyContainer in uncheckedContainers)
            {
                if (nearbyContainer is VanillaContainers vanillaContainers)
                {
                    Player? player = nearbyContainer?.m_nview.GetComponent<Player>();
                    // prevent claiming ownership of other players (e.g. through adventure backpacks)
                    if (!player || player == Player.m_localPlayer)
                    {
                        vanillaContainers?.m_nview.InvokeRPC("Autostore Ownership");
                    }
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
            foreach (IContainer c in Boxes.ContainersToPing)
            {
                PingContainer(c.gameObject);
                // Reset ownership of the container if you are the current owner and it's not the current container you have open.
                try
                {
                    if (c.IsOwner() && InventoryGui.instance && c != InventoryGui.instance.m_currentContainer)
                    {
                        c.m_nview.GetZDO().Set(ZDOVars.s_inUse, 0, false);
                        // Set it for client as well if the c can be cast to Container
                        if (c is VanillaContainers container)
                        {
                            container.gameObject.GetComponent<Container>().SetInUse(false);
                        }
                    }
                }
                catch (Exception e)
                {
                    AzuAutoStorePlugin.AzuAutoStoreLogger.LogError($"Error while trying to reset ownership of container {c.gameObject.name}: {e}");
                }
            }

            Boxes.ContainersToPing.Clear();
        }
    }


    internal static void PingContainer(GameObject container)
    {
        if (container == null) return;
        if (AzuAutoStorePlugin.PingContainers.Value == AzuAutoStorePlugin.Toggle.On && container.GetComponent<ChestPingEffect>() == null)
            container.AddComponent<ChestPingEffect>();

        if (AzuAutoStorePlugin.HighlightContainers.Value == AzuAutoStorePlugin.Toggle.On && container.GetComponent<HighLightChest>() == null)
            container.AddComponent<HighLightChest>();
    }


    internal static void LogDebug(string data)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug(data);
    }

    internal static void LogIfBuildDebug(string data)
    {
#if DEBUG
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug(data);
#endif
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