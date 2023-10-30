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
        // Check if the groups dictionary has been initialized
        if (AzuAutoStorePlugin.groups == null)
            AzuAutoStorePlugin.groups = new Dictionary<string, HashSet<string>>();

        if (AzuAutoStorePlugin.yamlData.TryGetValue("groups", out object groupData))
        {
            Dictionary<object, object>? groupDict = groupData as Dictionary<object, object>;
            if (groupDict != null)
            {
                foreach (KeyValuePair<object, object> group in groupDict)
                {
                    string groupName = group.Key.ToString();
                    if (group.Value is List<object> prefabs)
                    {
                        HashSet<string> prefabNames = new HashSet<string>();
                        foreach (object? prefab in prefabs)
                        {
                            prefabNames.Add(prefab.ToString());
                        }

                        AzuAutoStorePlugin.groups[groupName] = prefabNames;
                    }
                }
            }
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