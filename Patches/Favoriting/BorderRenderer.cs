using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AzuAutoStore.Patches.Favoriting;

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
static class BorderRenderer
{
    public static Sprite Border = null!;
    public const string BorderName = "AzuAutoStoreFavoritingBorder";

    [HarmonyPostfix]
    [HarmonyAfter("goldenrevolver.quick_stack_store")]
    [HarmonyPriority(Priority.LowerThanNormal)]
    static void UpdateGui(InventoryGrid __instance, Player player, Inventory ___m_inventory, List<InventoryGrid.Element> ___m_elements)
    {
        if (player == null || player.m_inventory != ___m_inventory)
        {
            return;
        }

        int width = ___m_inventory.GetWidth();
        UserConfig playerConfig = UserConfig.GetPlayerConfig(player.GetPlayerID());

        for (int y = 0; y < ___m_inventory.GetHeight(); ++y)
        {
            for (int x = 0; x < ___m_inventory.GetWidth(); ++x)
            {
                int index = y * width + x;

                Image img = ___m_elements[index].m_queued.transform.childCount > 0 && Utils.FindChild(___m_elements[index].m_queued.transform, BorderName) != null
                    ? Utils.FindChild(___m_elements[index].m_queued.transform, BorderName).GetComponent<Image>()
                    : CreateBorderImage(___m_elements[index].m_queued);

                img.color = AzuAutoStorePlugin.BorderColorFavoritedSlot.Value;
                img.enabled = playerConfig.IsSlotFavorited(new Vector2i(x, y));
            }
        }

        foreach (ItemDrop.ItemData itemData in ___m_inventory.m_inventory)
        {
            int index = itemData.GridVectorToGridIndex(width);

            Image img = ___m_elements[index].m_queued.transform.childCount > 0 && Utils.FindChild(___m_elements[index].m_queued.transform, BorderName) != null
                ? Utils.FindChild(___m_elements[index].m_queued.transform, BorderName).GetComponent<Image>()
                : CreateBorderImage(___m_elements[index].m_queued);

            bool isItemFavorited = playerConfig.IsItemNameFavorited(itemData.m_shared);
            if (isItemFavorited)
            {
                // enabled -> slot is favorited
                img.color = img.enabled ? AzuAutoStorePlugin.BorderColorFavoritedItemOnFavoritedSlot.Value : AzuAutoStorePlugin.BorderColorFavoritedItem.Value;

                // do this at the end of the if statement, so we can use img.enabled to deduce the slot favoriting
                img.enabled |= isItemFavorited;
            }
        }
    }

    private static Image CreateBorderImage(Image baseImg)
    {
        // set m_queued parent as parent first, so the position is correct
        Image? obj = Object.Instantiate(baseImg, baseImg.transform);
        // Destroy all children of the m_queued image just in case
        foreach (Transform child in obj.transform)
        {
            Object.Destroy(child.gameObject);
        }
        obj.name = BorderName;
        // change the parent to the m_queued image so we can access the new image without a loop
        obj.transform.SetParent(baseImg.transform);
        // set the new border image
        obj.sprite = Border;

        return obj;
    }
}

public static class ItemDataExtension
{
    public static int GridVectorToGridIndex(this ItemDrop.ItemData item, int width)
    {
        return item.m_gridPos.y * width + item.m_gridPos.x;
    }
}