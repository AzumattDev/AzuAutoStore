using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using AzuAutoStore.APIs.Compatibility.WardIsLove;
using AzuAutoStore.Interfaces;
using AzuAutoStore.Patches.Favoriting;
using AzuAutoStore.Util;
using HarmonyLib;
using UnityEngine;

namespace AzuAutoStore.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
internal static class ContainerAwakePatch
{
    internal static int pausedSeconds = 0;
    internal static readonly int storingPausedHash = "storingPaused".GetStableHashCode();

    private static void Postfix(Container __instance)
    {
        Functions.LogContainerStatus(__instance);
        
        if (__instance.m_nview.GetZDO() == null)
            return;

        if (__instance.m_nview)
        {
            __instance.m_nview.Register<bool>("RequestPause", (sender, pause) => Boxes.RPC_RequestPause(sender, pause, __instance));
            __instance.m_nview.Register("Autostore Ownership", uid => Boxes.RPC_Ownership(__instance, uid));
            __instance.m_nview.Register<bool>("Autostore OpenResponse", (_, response) => Boxes.RPC_OpenResponse(__instance, response));
        }

        if (__instance.m_nview.GetZDO().GetLong(ZDOVars.s_creator) == 0L || __instance.GetInventory() == null || !__instance.m_nview.IsValid())
            return;
        
        if (!__instance.m_nview.HasOwner())
        {
            __instance.m_nview.ClaimOwnership();
        }

        try
        {
            // Only add containers that the player should have access to
            if (WardIsLovePlugin.IsLoaded() && WardIsLovePlugin.WardEnabled()!.Value && WardMonoscript.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
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
            // ignored
        }
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
internal static class ContainerOnDestroyedPatch
{
    private static void Postfix(Container __instance)
    {
        if (__instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L || __instance.GetInventory() == null || !__instance.m_nview.IsValid())
            return;
        Boxes.RemoveContainer(__instance);
    }
}

[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnDestroy))]
static class WearNTearOnDestroyPatch
{
    static void Prefix(WearNTear __instance)
    {
        Container[]? container = __instance.GetComponentsInChildren<Container>();
        Container[]? parentContainer = __instance.GetComponentsInParent<Container>();
        if (container.Length > 0)
        {
            foreach (Container c in container)
            {
                Boxes.RemoveContainer(c);
            }
        }

        if (parentContainer.Length <= 0) return;
        {
            foreach (Container c in parentContainer)
            {
                Boxes.RemoveContainer(c);
            }
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
        if ((__instance.m_checkGuardStone && !PrivateArea.CheckAccess(__instance.transform.position)) || !__instance.CheckAccess(playerId))
            return;
        // Only add containers that the player should have access to
        if (WardIsLovePlugin.IsLoaded() && WardIsLovePlugin.WardEnabled()!.Value && WardMonoscript.CheckAccess(__instance.transform.position, flash: false, wardCheck: true))
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
            Functions.LogIfBuildDebug($"Found container by the name of {container.name} in your game.");
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
public static class PlayerUpdateTeleportPatchCleanupContainers
{
    public static void Prefix(float dt)
    {
        if (Player.m_localPlayer == null || !Player.m_localPlayer.m_teleporting)
            return;
        foreach (Container? container in Boxes.Containers.ToList().Where(container => container == null || container.transform == null || container.GetInventory() == null))
        {
            Boxes.RemoveContainer(container);
        }
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll), typeof(Inventory), typeof(bool))]
#if DEBUG
[HarmonyEmitIL]
#endif
public static class Inventory_StackAll_Patch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [..instructions];
        MethodInfo? getAllItemsMethod = typeof(Inventory).GetMethod(nameof(Inventory.GetAllItems), BindingFlags.Instance | BindingFlags.Public, null, [], null);

        int getAllItemsIndex = code.FindIndex(instr => instr.opcode == OpCodes.Callvirt && ReferenceEquals(instr.operand, getAllItemsMethod));

        if (getAllItemsIndex == -1)
        {
            throw new System.Exception("Could not find GetAllItems call");
        }

        List<CodeInstruction> newInstructions =
        [
            // Call GetAllItems
            new CodeInstruction(OpCodes.Callvirt, getAllItemsMethod),

            // Store the result in a local variable (list of items)
            new CodeInstruction(OpCodes.Stloc_3),

            // Load the list of items onto the stack
            new CodeInstruction(OpCodes.Ldloc_3),

            // Create a new list with filtered items
            new CodeInstruction(OpCodes.Call, typeof(Inventory_StackAll_Patch).GetMethod(nameof(FilterItems))),

            // Store the filtered list back into the local variable
            new CodeInstruction(OpCodes.Stloc_3),

            // Load the filtered list for the next instructions
            new CodeInstruction(OpCodes.Ldloc_3)
        ];

        // Replace the GetAllItems call with our new instructions
        code.RemoveRange(getAllItemsIndex, 2); // Remove the original GetAllItems call and the newobj instruction
        code.InsertRange(getAllItemsIndex, newInstructions);
        return code.AsEnumerable();
    }

    public static List<ItemDrop.ItemData> FilterItems(List<ItemDrop.ItemData> items)
    {
        return items.Where(ShouldIncludeItem).ToList();
    }

    public static bool ShouldIncludeItem(ItemDrop.ItemData item)
    {
        return !VanillaContainers.CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID()));
    }
}