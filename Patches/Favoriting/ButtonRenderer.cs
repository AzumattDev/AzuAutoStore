using System.Collections;
using UnityEngine.UI;

namespace AzuAutoStore.Patches.Favoriting
{
    internal class ButtonRenderer
    {
        internal static bool hasOpenedInventoryOnce = false;
        
        internal static Button favoritingTogglingButton = null!;

        internal class MainButtonUpdate
        {
            internal static void UpdateInventoryGuiButtons(InventoryGui __instance)
            {
                if (!hasOpenedInventoryOnce)
                {
                    return;
                }

                if (__instance != InventoryGui.instance)
                {
                    return;
                }

                if (Player.m_localPlayer)
                {
                    // reset in case player forgot to turn it off
                    FavoritingMode.HasCurrentlyToggledFavoriting = false;
                }
            }
        }

        /// <summary>
        /// Wait one frame for Destroy to finish, then reset UI
        /// </summary>
        internal static IEnumerator WaitAFrameToUpdateUIElements(InventoryGui instance, bool includeTrashButton)
        {
            yield return null;

            if (instance == null)
            {
                yield break;
            }

            MainButtonUpdate.UpdateInventoryGuiButtons(instance);
        }
    }
}