using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.Util;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AzuAutoStore;

// Patch the Terminal.Init to add commands to the terminal
[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
static class TerminalInitTerminalPatch
{
    private const float SearchRadius = 50f;

    public static void GetAllPiecesInRadius(Vector3 p, float radius, List<Piece> pieces)
    {
        if (Piece.s_ghostLayer == 0)
            Piece.s_ghostLayer = LayerMask.NameToLayer("ghost");
        foreach (Piece allPiece in Piece.s_allPieces)
        {
            if (allPiece.gameObject.layer != Piece.s_ghostLayer && (double) Vector3.Distance(p, allPiece.transform.position) < (double) radius)
                pieces.Add(allPiece);
        }
    }
    static void Postfix(Terminal __instance)
    {
        Terminal.ConsoleCommand searchNearbyCwItems = new("azuautostoresearch",
            "[prefab name] - search for items in chests near the player. It uses the prefab name",
            args =>
            {
                if (args.Length <= 1 || !ZNetScene.instance)
                    return;

                SearchNearbyContainersFor(args[1]);


                static void SearchNearbyContainersFor(string query)
                {
                    foreach (Piece piece in GetNearbyMatchingPieces(query))
                    {
                        Functions.PingContainer(piece);
                    }
                }

                static IEnumerable<Piece> GetNearbyMatchingPieces(string query)
                {
                    List<Piece> pieces = new();

                    GetAllPiecesInRadius(
                        Player.m_localPlayer.transform.position,
                        SearchRadius,
                        pieces
                    );

                    return pieces
                        .Where(p => p.GetComponent<Container>())
                        .Where(p => ContainerContainsMatchingItem(p, query));
                }

                static bool ContainerContainsMatchingItem(Component container, string query)
                {
                    return container
                        .GetComponent<Container>()
                        .GetInventory()
                        .GetAllItems()
                        .Any(i => NormalizedItemName(i).Contains(query));
                }

                static string NormalizedItemName(ItemDrop.ItemData itemData)
                {
                    return Utils.GetPrefabName(itemData.m_dropPrefab);
                }
            },
            optionsFetcher: () =>
                !(bool)(Object)ZNetScene.instance
                    ? new List<string>()
                    : ZNetScene.instance.GetPrefabNames());
    }
}