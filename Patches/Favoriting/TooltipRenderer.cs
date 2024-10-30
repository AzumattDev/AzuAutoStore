using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace AzuAutoStore.Patches.Favoriting
{
    [HarmonyPatch(typeof(ItemDrop.ItemData))]
    internal static class TooltipRenderer
    {
        [HarmonyPatch(nameof(ItemDrop.ItemData.GetTooltip), new Type[]
        {
            typeof(ItemDrop.ItemData),
            typeof(int),
            typeof(bool),
            typeof(float),
            typeof(int)
        })]
        [HarmonyPostfix]
        public static void GetTooltip(ItemDrop.ItemData item, bool crafting, ref string __result)
        {
            if (crafting || !AzuAutoStorePlugin.DisplayTooltipHint.Value)
            {
                return;
            }
            StringBuilder stringBuilder = new StringBuilder(256);
            stringBuilder.Append(__result);

            UserConfig conf = UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID());

            if (conf.IsItemNameFavorited(item.m_shared))
            {
                string? color = ColorUtility.ToHtmlStringRGB(AzuAutoStorePlugin.BorderColorFavoritedItem.Value);

                stringBuilder.Append($"{Environment.NewLine}<color=#{color}>{AzuAutoStorePlugin.FavoritedItemTooltip.Value}</color>");
            }
            else if (conf.IsSlotFavorited(item.m_gridPos))
            {
                string? color = ColorUtility.ToHtmlStringRGB(AzuAutoStorePlugin.BorderColorFavoritedSlot.Value);

                stringBuilder.Append($"{Environment.NewLine}<color=#{color}>{AzuAutoStorePlugin.FavoritedSlotTooltip.Value}</color>");
            }
            else if (conf.IsSlotFavorited(item.m_gridPos) && conf.IsItemNameFavorited(item.m_shared))
            {
                string? color = ColorUtility.ToHtmlStringRGB(AzuAutoStorePlugin.BorderColorFavoritedItemOnFavoritedSlot.Value);

                stringBuilder.Append($"{Environment.NewLine}<color=#{color}>{AzuAutoStorePlugin.ItemOnFavoritedSlotTooltip.Value}</color>");
            }

            __result = stringBuilder.ToString();
        }
    }
}