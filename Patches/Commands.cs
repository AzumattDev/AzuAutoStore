using System;
using System.Collections.Generic;
using System.Linq;
using AzuAutoStore.Interfaces;
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
            if (allPiece.gameObject.layer != Piece.s_ghostLayer && (double)Vector3.Distance(p, allPiece.transform.position) < (double)radius)
                pieces.Add(allPiece);
        }
    }

    static void Postfix(Terminal __instance)
    {
        Terminal.ConsoleCommand searchNearbyCwItems = new("azuautostoresearch",
            "[prefab name/query text] - search for items in chests near the player. It uses the prefab name",
            args =>
            {
                if (args.Length <= 1 || !ZNetScene.instance)
                    return;

                int itemCount = 0;
                bool itemFound = SearchNearbyContainersFor(args[1]);
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, itemFound
                    ? $"Found {itemCount} items matching '{args[1]}' in nearby containers."
                    : $"<color=red>No items matching '{args[1]}' found in nearby containers.</color>");


                bool SearchNearbyContainersFor(string query)
                {
                    bool found = false;
                    IContainer closestPiece = null!;
                    float closestDistance = float.MaxValue;
                    foreach (IContainer piece in GetNearbyMatchingPieces(query))
                    {
                        Functions.PingContainer(piece.gameObject);
                        found = true;
                        float distance = Vector3.Distance(piece.gameObject.transform.position, Player.m_localPlayer.transform.position);
                        if (!(distance < closestDistance)) continue;
                        closestDistance = distance;
                        closestPiece = piece;
                    }

                    if (closestPiece != null)
                    {
                        Vector3 pos = closestPiece.gameObject.transform.position;
                        Player.m_localPlayer.SetLookDir(pos - Player.m_localPlayer.transform.position, 3.5f);
                    }

                    return found;
                }

                IEnumerable<IContainer> GetNearbyMatchingPieces(string query)
                {
                    List<Piece> pieces = new();

                    GetAllPiecesInRadius(
                        Player.m_localPlayer.transform.position,
                        SearchRadius,
                        pieces
                    );
                    IEnumerable<IContainer> drawersCheck = APIs.ItemDrawers_API.AllDrawers.Where(x => string.Equals(x.Prefab, query, StringComparison.CurrentCultureIgnoreCase)).Select(kgDrawer.Create);
                    return pieces
                        .Where(p => p.GetComponent<Container>())
                        .Where(p => ContainerContainsMatchingItem(p, query.ToLower(), ref itemCount)).Select(p => VanillaContainers.Create(p.GetComponent<Container>())).Concat(drawersCheck);
                }

                static bool ContainerContainsMatchingItem(Component container, string query, ref int count)
                {
                    Inventory? inventory = container.GetComponent<Container>().GetInventory();
                    IEnumerable<ItemDrop.ItemData> matchingItems = inventory.GetAllItems().Where(i => NormalizedItemName(i).Contains(query) || i.m_shared.m_name.ToLower().Contains(query));

                    IEnumerable<ItemDrop.ItemData> itemDatas = matchingItems.ToList();
                    count += itemDatas.Sum(item => item.m_stack);

                    return itemDatas.Any();
                }

                static string NormalizedItemName(ItemDrop.ItemData itemData)
                {
                    return Utils.GetPrefabName(itemData.m_dropPrefab).ToLower();
                }
            },
            optionsFetcher: () =>
                !(bool)(Object)ZNetScene.instance
                    ? new List<string>()
                    : ZNetScene.instance.GetPrefabNames());
    }
}