using System;
using System.Linq;
using AzuAutoStore.Util;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AzuAutoStore.Managers;

[HarmonyPatch(typeof(Game), nameof(Game.Start))]
public static class GameStartPatch
{
    static void Postfix()
    {
        AzuAutoStoreItemDropManager? itemDropManager = new GameObject("AzuAutoStore_ItemDropManager").AddComponent<AzuAutoStoreItemDropManager>();
        Object.DontDestroyOnLoad(itemDropManager);
    }
}

public class AzuAutoStoreItemDropManager : MonoBehaviour
{
    private void Awake()
    {
        InvokeRepeating(nameof(ProcessItemDrops), AzuAutoStorePlugin.IntervalSeconds.Value, AzuAutoStorePlugin.IntervalSeconds.Value);
        AzuAutoStorePlugin.IntervalSeconds.SettingChanged += OnIntervalSecondsChanged;
    }

    private void OnDestroy()
    {
        AzuAutoStorePlugin.IntervalSeconds.SettingChanged -= OnIntervalSecondsChanged;
    }

    private void ProcessItemDrops()
    {
        if (ShouldPause())
        {
            return;
        }

        if (Boxes.Containers == null || Boxes.Containers.Count == 0)
            return;

        foreach (ItemDrop itemDrop in ItemDrop.s_instances.Where(x => !x.IsPiece()).ToList())
        {
            if (itemDrop == null || itemDrop.transform == null || itemDrop.m_nview == null || !itemDrop.m_nview.IsValid())
                continue;

            Functions.CheckItemDropInstanceAndStore(itemDrop);
        }
    }

    private void OnIntervalSecondsChanged(object sender, EventArgs e)
    {
        CancelInvoke(nameof(ProcessItemDrops));
        InvokeRepeating(nameof(ProcessItemDrops), AzuAutoStorePlugin.IntervalSeconds.Value, AzuAutoStorePlugin.IntervalSeconds.Value);
    }

    private static bool ShouldPause()
    {
        Player? player = Player.m_localPlayer;
        return player == null || player.IsTeleporting() || player.IsDead();
    }
}