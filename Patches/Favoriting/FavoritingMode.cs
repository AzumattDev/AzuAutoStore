namespace AzuAutoStore.Patches.Favoriting
{
    internal class FavoritingMode
    {
        private static bool hasCurrentlyToggledFavoriting = false;

        internal static bool HasCurrentlyToggledFavoriting
        {
            get => hasCurrentlyToggledFavoriting;
            set { hasCurrentlyToggledFavoriting = value; }
        }

        internal static void RefreshDisplay()
        {
            HasCurrentlyToggledFavoriting |= false;
        }

        internal static bool IsInFavoritingMode()
        {
            return HasCurrentlyToggledFavoriting
                   || AzuAutoStorePlugin.FavoritingModifierKeybind1.Value.IsKeyHeld()
                   || AzuAutoStorePlugin.FavoritingModifierKeybind2.Value.IsKeyHeld() || AzuAutoStorePlugin.SearchModifierKeybind.Value.IsKeyHeld();
        }
    }
}