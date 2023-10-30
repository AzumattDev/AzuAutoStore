using HarmonyLib;

namespace AzuAutoStore.Patches.Favoriting;

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
static class InventoryGuiShowPatch
{
    // slightly lower priority so we get rendered on top of equipment slot mods
    [HarmonyPriority(Priority.LowerThanNormal)]
    static void Postfix(InventoryGui __instance)
    {
        ButtonRenderer.hasOpenedInventoryOnce = true;

        ButtonRenderer.MainButtonUpdate.UpdateInventoryGuiButtons(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
static class InventoryGuiHidePatch
{
    [HarmonyPriority(Priority.LowerThanNormal)]
    static void Postfix(InventoryGui __instance)
    {
        // reset in case player forgot to turn it off
        FavoritingMode.HasCurrentlyToggledFavoriting = false;
    }
}