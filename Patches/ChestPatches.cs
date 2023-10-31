using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AzuAutoStore.Util;
using AzuAutoStore.Util.Compatibility.WardIsLove;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AzuAutoStore.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
internal static class ContainerAwakePatch
{
    internal static Dictionary<Container, Coroutine> containerCoroutines = new();
    internal static int pausedSeconds = 0;
    internal static int lastCount = 0;
    internal static readonly int storingPausedHash = "storingPaused".GetStableHashCode();

    private static void Postfix(Container __instance)
    {
        if (__instance.m_nview.GetZDO() != null)
        {
            __instance.m_nview.Register<bool>("RequestPause", (sender, pause) => Boxes.RPC_RequestPause(sender, pause, __instance));
            __instance.m_nview.Register("Autostore Ownership", uid => Boxes.RPC_Ownership(__instance, uid));
            __instance.m_nview.Register<bool>("Autostore OpenResponse", (_, response) => Boxes.RPC_OpenResponse(__instance, response));
        }

        if (__instance.name.StartsWith("Treasure") || __instance.GetInventory() == null || !__instance.m_nview.IsValid())
            return;

        try
        {
            // Only add containers that the player should have access to
            if (WardIsLovePlugin.IsLoaded() && WardIsLovePlugin.WardEnabled().Value &&
                WardMonoscript.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
            {
                Boxes.AddContainer(__instance);
            }
            else
            {
                if (PrivateArea.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
                    Boxes.AddContainer(__instance);
            }
        }
        catch
        {
        }

        Coroutine newCoroutine = __instance.StartCoroutine(PeriodicCheck(__instance));
        containerCoroutines[__instance] = newCoroutine;
    }

    private static IEnumerator PeriodicCheck(Container containerInstance, bool execNext = false)
    {
        float regularSearchInterval = 10.0f;
        float quickSearchInterval = 0.0f;
        float currentInterval = regularSearchInterval;

        while (true)
        {
            bool storingPaused = containerInstance.m_nview.GetZDO().GetBool(storingPausedHash);

            if (storingPaused)
            {
                pausedSeconds++;
                AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"Paused for {pausedSeconds} seconds");

                if (pausedSeconds >= AzuAutoStorePlugin.SecondsToWaitBeforeStoring.Value)
                {
                    containerInstance.m_nview.GetZDO().Set(storingPausedHash, false);
                    AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"Paused for {pausedSeconds} seconds, resuming");
                    pausedSeconds = 0;
                }

                yield return new WaitForSeconds(1);
            }
            else
            {
                if (execNext)
                {
                    execNext = false;
                    currentInterval = quickSearchInterval;
                }

                yield return new WaitForSeconds(currentInterval);

                foreach (Collider? collider in Physics.OverlapSphere(containerInstance.transform.position, Functions.GetContainerRange(containerInstance), LayerMask.GetMask("item")))
                    if (collider?.attachedRigidbody)
                    {
                        ItemDrop? item = collider.attachedRigidbody.GetComponent<ItemDrop>();
                        if (!item) continue;
                        Functions.LogDebug($"Nearby item name: {item.m_itemData.m_dropPrefab.name}");

                        if (item?.GetComponent<ZNetView>()?.IsValid() != true ||
                            !item.GetComponent<ZNetView>().IsOwner())
                            continue;
                        if (!Functions.TryStore(containerInstance, ref item.m_itemData)) continue;
                        item.Save();
                        if (item.m_itemData.m_stack <= 0)
                        {
                            if (item.GetComponent<ZNetView>() == null)
                                Object.DestroyImmediate(item.gameObject);
                            else
                                ZNetScene.instance.Destroy(item.gameObject);
                        }
                    }


                currentInterval = regularSearchInterval;
            }
        }
    }

    public static void ItemDroppedNearby(Container containerInstance)
    {
        if (containerCoroutines.TryGetValue(containerInstance, out var existingCoroutine))
        {
            containerInstance.StopCoroutine(existingCoroutine);
            Coroutine newCoroutine = containerInstance.StartCoroutine(PeriodicCheck(containerInstance, true));
            containerCoroutines[containerInstance] = newCoroutine;
        }
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
internal static class ContainerOnDestroyedPatch
{
    private static void Postfix(Container __instance)
    {
        if (__instance.name.StartsWith("Treasure") || __instance.GetInventory() == null ||
            !__instance.m_nview.IsValid() ||
            __instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L)
            return;
        Boxes.RemoveContainer(__instance);

        try
        {
            if (ContainerAwakePatch.containerCoroutines.ContainsKey(__instance))
            {
                ContainerAwakePatch.containerCoroutines.Remove(__instance);
                __instance.StopCoroutine(ContainerAwakePatch.containerCoroutines[__instance]);
            }
        }
        catch (Exception exception)
        {
            Functions.LogError($"Error in ContainerOnDestroyedPatch Couldn't remove container coroutine: {exception}");
        }
    }
}

// Add container to list on container interaction. Just in case.
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
static class ContainerInteractPatch
{
    static void Postfix(Container __instance, Humanoid character, bool hold, bool alt)
    {
        long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
        if ((__instance.m_checkGuardStone && !PrivateArea.CheckAccess(__instance.transform.position)) ||
            !__instance.CheckAccess(playerId))
            return;
        // Only add containers that the player should have access to
        if (WardIsLovePlugin.IsLoaded() && WardIsLovePlugin.WardEnabled()!.Value &&
            WardMonoscript.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
        {
            Boxes.AddContainer(__instance);
        }
        else
        {
            if (PrivateArea.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
                Boxes.AddContainer(__instance);
        }
    }
}

// Postfix ZNetScene Awake to get all containers loaded by the game.
[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
internal static class ZNetSceneAwakePatch
{
    private static void Postfix(ZNetScene __instance)
    {
        foreach (Container? container in Resources.FindObjectsOfTypeAll<Container>())
        {
            Functions.LogDebug($"Found container by the name of {container.name} in your game.");
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
public static class PlayerUpdateTeleportPatchCleanupContainers
{
    public static void Prefix(float dt)
    {
        if (!(Player.m_localPlayer != null) || !Player.m_localPlayer.m_teleporting)
            return;
        foreach (Container container in Boxes.Containers.ToList().Where(container =>
                     (!(container != null) || !(container.transform != null)
                         ? 0
                         : (container.GetInventory() != null ? 1 : 0)) == 0).Where(container => container != null))
        {
            Boxes.RemoveContainer(container);
        }
    }
}