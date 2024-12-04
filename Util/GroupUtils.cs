using System.Collections.Generic;
using System.Linq;

namespace AzuAutoStore.Util;

public class GroupUtils
{
    // Get a list of all excluded groups for a container
    public static List<string> GetExcludedGroups(string container)
    {
        if (AzuAutoStorePlugin.yamlData != null && AzuAutoStorePlugin.yamlData.TryGetValue(container, out object containerData))
        {
            Dictionary<object, object>? containerInfo = containerData as Dictionary<object, object>;
            if (containerInfo != null && containerInfo.TryGetValue("exclude", out object excludeData))
            {
                List<object>? excludeList = excludeData as List<object>;
                if (excludeList != null)
                {
                    return excludeList.Where(excludeItem =>
                            AzuAutoStorePlugin.groups.ContainsKey(excludeItem.ToString()))
                        .Select(excludeItem => excludeItem.ToString()).ToList();
                }
            }
        }

        return [];
    }

    public static bool IsGroupDefined(string? groupName)
    {
        if (AzuAutoStorePlugin.yamlData == null)
        {
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogError("yamlData is null. Make sure that your YAML file is not empty or to call DeserializeYamlFile() before using IsGroupDefined.");
            return false;
        }

        bool groupInYaml = false;

        if (AzuAutoStorePlugin.yamlData.ContainsKey("groups"))
        {
            Dictionary<object, object>? groupsData = AzuAutoStorePlugin.yamlData["groups"] as Dictionary<object, object>;
            if (groupsData != null)
            {
                if (groupName != null) groupInYaml = groupsData.ContainsKey(groupName);
            }
            else
            {
                AzuAutoStorePlugin.AzuAutoStoreLogger.LogError("Unable to cast groupsData to Dictionary<object, object>.");
            }
        }

        // Check for the group in both yamlData and predefined groups
        return groupInYaml || AzuAutoStorePlugin.groups.ContainsKey(groupName);
    }


// Check if a group exists in the container data
    public static bool GroupExists(string? groupName)
    {
        return AzuAutoStorePlugin.groups.ContainsKey(groupName);
    }

// Get a list of all groups in the container data
    public static List<string?> GetAllGroups()
    {
        return AzuAutoStorePlugin.groups.Keys.ToList();
    }

// Get a list of all items in a group
    public static List<string?> GetItemsInGroup(string? groupName)
    {
        if (AzuAutoStorePlugin.groups.TryGetValue(groupName, out HashSet<string?> groupPrefabs))
        {
            return groupPrefabs.ToList();
        }

        return [];
    }

    /*public static bool IsItemInGroup(string itemName, string groupName)
    {
        if (PredefinedGroups.ContainsKey(groupName))
        {
            return PredefinedGroups[groupName].Items.Contains(itemName);
        }

        return false;
    }*/
}