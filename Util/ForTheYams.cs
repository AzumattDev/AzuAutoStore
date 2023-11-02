using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace AzuAutoStore.Util;

public static class YamlUtils
{
    internal static void ReadYaml(string yamlInput)
    {
        IDeserializer? deserializer = new DeserializerBuilder().Build();
        AzuAutoStorePlugin.yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlInput);
        AzuAutoStorePlugin.AzuAutoStoreLogger.LogDebug($"yamlData:\n{yamlInput}");
    }

    internal static void ParseGroups()
    {
        // Initialize the groups dictionary if it's null
        AzuAutoStorePlugin.groups ??= new Dictionary<string?, HashSet<string?>>();

        // Validate yamlData before trying to use it
        if (AzuAutoStorePlugin.yamlData == null)
        {
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogError("yamlData is null.");
            return;
        }

        if (AzuAutoStorePlugin.yamlData.TryGetValue("groups", out object groupData))
        {
            // Safely cast to the expected Dictionary type
            if (groupData is Dictionary<object, object> groupDict)
            {
                foreach (KeyValuePair<object, object> group in groupDict)
                {
                    string? groupName = group.Key?.ToString();
                    if (groupName == null) continue; // Skip if the key can't be converted to string

                    // Safely cast to the expected List type
                    if (group.Value is List<object> prefabs)
                    {
                        HashSet<string?> prefabNames = new();

                        foreach (object prefab in prefabs)
                        {
                            string? prefabName = prefab?.ToString();
                            if (prefabName != null)
                            {
                                prefabNames.Add(prefabName);
                            }
                        }

                        AzuAutoStorePlugin.groups[groupName] = prefabNames;
                    }
                }
            }
            else
            {
                AzuAutoStorePlugin.AzuAutoStoreLogger.LogError("groupData is not of type Dictionary<object, object>.");
            }
        }
        else
        {
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogError("No 'groups' key found in yamlData.");
        }
    }

    public static void WriteYaml(string filePath)
    {
        ISerializer? serializer = new SerializerBuilder().Build();
        using StreamWriter? output = new StreamWriter(filePath);
        serializer.Serialize(output, AzuAutoStorePlugin.yamlData);

        // Serialize the data again to YAML format
        string serializedData = serializer.Serialize(AzuAutoStorePlugin.yamlData);

        // Append the serialized YAML data to the file
        File.AppendAllText(filePath, serializedData);
    }
}