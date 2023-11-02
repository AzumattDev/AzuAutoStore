using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AzuAutoStore.Util;

public class MiscFunctions
{
    private static void LogResourceInfo(int totalAmount, int totalRequirement, string reqName)
    {
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"(ConsumeResourcesPatch) Have {totalAmount}/{totalRequirement} {reqName} in player inventory");
    }

    public static string GetPrefabName(string name)
    {
        char[] anyOf = new char[2] { '(', ' ' };
        int length = name.IndexOfAny(anyOf);
        return length < 0 ? name : name.Substring(0, length);
    }

    internal static GameObject? GetItemPrefabFromGameObject(ItemDrop itemDropComponent, GameObject inputGameObject)
    {
        GameObject? itemPrefab = ObjectDB.instance.GetItemPrefab(GetPrefabName(inputGameObject.name));
        itemDropComponent.m_itemData.m_dropPrefab = itemPrefab;
        return itemPrefab != null ? itemPrefab : null;
    }

    internal static bool CheckItemDropIntegrity(ItemDrop itemDropComp)
    {
        if (itemDropComp.m_itemData == null) return false;
        return itemDropComp.m_itemData.m_shared != null;
    }

    internal static bool CheckItemSharedIntegrity(ItemDrop.ItemData itemData)
    {
        return itemData.m_shared != null;
    }

    internal static void CreatePredefinedGroups(ObjectDB __instance)
    {
        foreach (GameObject gameObject in __instance.m_items.Where(x => x.GetComponentInChildren<ItemDrop>() != null))
        {
            ItemDrop? itemDrop = gameObject.GetComponentInChildren<ItemDrop>();
            if (!CheckItemDropIntegrity(itemDrop)) continue;
            GameObject? drop = GetItemPrefabFromGameObject(itemDrop, gameObject);
            itemDrop.m_itemData.m_dropPrefab = itemDrop.gameObject; // Fix all drop prefabs to be the actual item
            if (drop != null)
            {
                ItemDrop.ItemData.SharedData sharedData = itemDrop.m_itemData.m_shared;
                List<string?> groupNames = new();

                if (sharedData.m_food > 0.0 && sharedData.m_foodStamina > 0.0)
                {
                    groupNames.Add("Food");
                }

                if (sharedData.m_consumeStatusEffect != null)
                {
                    if ((sharedData.m_food > 0.0 && sharedData.m_foodStamina == 0.0)
                        || sharedData.m_consumeStatusEffect.name.ToLower().Contains("potion")
                        || sharedData.m_consumeStatusEffect.m_name.ToLower().Contains("potion")
                        || sharedData.m_consumeStatusEffect.m_category.ToLower().Contains("potion")
                        || sharedData.m_ammoType.ToLower().Contains("mead"))
                    {
                        groupNames.Add("Potion");
                    }
                    else if (sharedData.m_itemType == ItemDrop.ItemData.ItemType.Fish)
                    {
                        groupNames.Add("Fish");
                    }
                }

                switch (sharedData.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
                        or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow:
                        bool isWeapon = true;
                        switch (sharedData.m_skillType)
                        {
                            case Skills.SkillType.Swords:
                                groupNames.Add("Swords");
                                break;
                            case Skills.SkillType.Bows:
                                groupNames.Add("Bows");
                                break;
                            case Skills.SkillType.Crossbows:
                                groupNames.Add("Crossbows");
                                break;
                            case Skills.SkillType.Axes:
                                groupNames.Add("Axes");
                                break;
                            case Skills.SkillType.Clubs:
                                groupNames.Add("Clubs");
                                break;
                            case Skills.SkillType.Knives:
                                groupNames.Add("Knives");
                                break;
                            case Skills.SkillType.Pickaxes:
                                groupNames.Add("Pickaxes");
                                break;
                            case Skills.SkillType.Polearms:
                                groupNames.Add("Polearms");
                                break;
                            case Skills.SkillType.Spears:
                                groupNames.Add("Spears");
                                break;
                            case Skills.SkillType.ElementalMagic:
                                groupNames.Add("ElementalMagic");
                                break;
                            case Skills.SkillType.BloodMagic:
                                groupNames.Add("BloodMagic");
                                break;
                            default:
                                isWeapon = false;
                                break;
                        }

                        if (isWeapon)
                        {
                            groupNames.Add("Weapons");
                        }

                        break;
                    case ItemDrop.ItemData.ItemType.Shield:
                        groupNames.Add("Shield");
                        groupNames.Add(sharedData.m_timedBlockBonus > 0.0 ? "Round Shield" : "Tower Shield");
                        break;
                    case ItemDrop.ItemData.ItemType.Helmet:
                        groupNames.AddRange(new[] { "Armor", "Helmet" });
                        break;
                    case ItemDrop.ItemData.ItemType.Chest:
                        groupNames.AddRange(new[] { "Armor", "Chest" });
                        break;
                    case ItemDrop.ItemData.ItemType.Legs:
                        groupNames.AddRange(new[] { "Armor", "Legs" });
                        break;
                    case ItemDrop.ItemData.ItemType.Shoulder:
                        groupNames.AddRange(new[] { "Armor", "Shoulder" });
                        break;
                    case ItemDrop.ItemData.ItemType.Utility:
                        groupNames.AddRange(new[] { "Utility" });
                        break;
                    case ItemDrop.ItemData.ItemType.Ammo:
                        string ammoType = sharedData.m_ammoType;
                        if (ammoType != "$ammo_bolts")
                        {
                            if (ammoType == "$ammo_arrows")
                                groupNames.Add("Arrows");
                        }
                        else
                            groupNames.Add("Bolts");

                        groupNames.Add("Ammo");
                        break;
                    case ItemDrop.ItemData.ItemType.Torch or ItemDrop.ItemData.ItemType.Bow
                        or ItemDrop.ItemData.ItemType.Shield or ItemDrop.ItemData.ItemType.Tool
                        or ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
                        or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Helmet
                        or ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs
                        or ItemDrop.ItemData.ItemType.Shoulder or ItemDrop.ItemData.ItemType.Utility:
                        groupNames.Add("Equipment");
                        break;
                    case ItemDrop.ItemData.ItemType.Trophy:
                        string[] bossTrophies = { "eikthyr", "elder", "bonemass", "dragonqueen", "goblinking", "SeekerQueen" };
                        groupNames.Add(bossTrophies.Any(sharedData.m_name.EndsWith) ? "Boss Trophy" : "Trophy");
                        break;
                    case ItemDrop.ItemData.ItemType.Material:
                        if (ObjectDB.instance.GetItemPrefab("Cultivator").GetComponent<ItemDrop>().m_itemData.m_shared
                                .m_buildPieces.m_pieces.FirstOrDefault(p =>
                                {
                                    Piece.Requirement[] requirements = p.GetComponent<Piece>().m_resources;
                                    return requirements.Length == 1 &&
                                           requirements[0].m_resItem.m_itemData.m_shared.m_name == sharedData.m_name;
                                }) is { } piece)
                        {
                            groupNames.Add(piece.GetComponent<Plant>()?.m_grownPrefabs[0].GetComponent<Pickable>()
                                ?.m_amount > 1
                                ? "Crops"
                                : "Seeds");
                        }

                        if (ZNetScene.instance.GetPrefab("smelter").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupNames.Add("Ores");
                        }

                        if (ZNetScene.instance.GetPrefab("smelter").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_to.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupNames.Add("Metals");
                        }

                        if (ZNetScene.instance.GetPrefab("blastfurnace").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupNames.Add("Ores");
                        }

                        if (ZNetScene.instance.GetPrefab("blastfurnace").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_to.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupNames.Add("Metals");
                        }

                        if (ZNetScene.instance.GetPrefab("charcoal_kiln").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupNames.Add("Woods");
                        }

                        if (sharedData.m_name == "$item_elderbark")
                        {
                            groupNames.Add("Woods");
                        }

                        break;
                }

                foreach (string? groupName in groupNames)
                {
                    if (!string.IsNullOrEmpty(groupName))
                    {
                        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"(CreatePredefinedGroups) Adding {itemDrop.m_itemData.m_dropPrefab.name} to {groupName}");
                        AddItemToGroup(groupName, itemDrop);
                    }
                }

                if (sharedData != null)
                {
                    AddItemToGroup("All", itemDrop);
                }
            }
        }

        SaveGroupsToFile();
    }

    // Store a flag indicating whether any group data has changed. This can help minimize disk writes.
    private static bool dataChanged = false;

    private static void AddItemToGroup(string? groupName, ItemDrop itemDrop)
    {
        // Check if the group exists, and if not, create it
        if (!GroupUtils.GroupExists(groupName))
        {
            AzuAutoStorePlugin.groups[groupName] = new HashSet<string?>();
        }

        // Add the item to the group
        string? prefabName = Utils.GetPrefabName(itemDrop.m_itemData.m_dropPrefab);
        if (AzuAutoStorePlugin.groups[groupName].Contains(prefabName)) return;
        AzuAutoStorePlugin.groups[groupName].Add(prefabName);
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"(CreatePredefinedGroups) Added {prefabName} to {groupName}");
    }

    private static void SaveGroupsToFile()
    {
        if (!dataChanged)
        {
            return;
        }

        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, $"{AzuAutoStorePlugin.ModName}_PredefinedGroups.txt");
        using StreamWriter file = new(path, false);

        // Before writing to the file, alphabetize the groups then the prefab names
        foreach (string? group in AzuAutoStorePlugin.groups.Keys.OrderBy(x => x))
        {
            file.WriteLine(group);
            foreach (string prefab in AzuAutoStorePlugin.groups[group].OrderBy(x => x))
            {
                file.WriteLine($"\t{prefab}");
            }
        }

        dataChanged = false;
    }
}