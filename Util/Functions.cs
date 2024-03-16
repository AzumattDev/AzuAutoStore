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

 

    internal static bool TryStore(Container nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false)
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
            foreach (IContainer c in Boxes.ContainersToPing)
            {
                PingContainer(c.gameObject);
            }

            Boxes.ContainersToPing.Clear();
        }
    }


    internal static void PingContainer(GameObject container)
    {
        if (AzuAutoStorePlugin.PingContainers.Value == AzuAutoStorePlugin.Toggle.On && container.GetComponent<ChestPingEffect>() == null)
            container.AddComponent<ChestPingEffect>();

        if (AzuAutoStorePlugin.HighlightContainers.Value == AzuAutoStorePlugin.Toggle.On && container.GetComponent<HighLightChest>() == null)
            container.AddComponent<HighLightChest>();
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