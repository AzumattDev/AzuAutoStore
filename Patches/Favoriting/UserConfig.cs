using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using BepInEx;

namespace AzuAutoStore.Patches.Favoriting
{
    public class UserConfig
    {
        private static readonly Dictionary<long, UserConfig> PlayerConfigs = new Dictionary<long, UserConfig>();

        public static UserConfig GetPlayerConfig(long playerID)
        {
            if (PlayerConfigs.TryGetValue(playerID, out UserConfig userConfig))
            {
                return userConfig;
            }
            else
            {
                userConfig = new UserConfig(playerID);
                PlayerConfigs[playerID] = userConfig;

                return userConfig;
            }
        }

        /// <summary>
        /// Create a user config for this local save file
        /// </summary>
        public UserConfig(long uid)
        {
            _configPath = Path.Combine(Paths.ConfigPath, $"{AzuAutoStorePlugin.ModName}_player_{uid}.dat");
            Load();
        }

        internal void ResetAllFavoriting()
        {
            AzuAutoStorePlugin.AzuAutoStoreLogger.LogWarning("Resetting all favoriting data!");

            _favoritedSlots = new HashSet<Vector2i>();
            _favoritedItems = new HashSet<string>();

            Save();
        }

        private void Save()
        {
            using Stream stream = File.Open(_configPath, FileMode.Create);
            List<Tuple<int, int>>? tupledSlots = _favoritedSlots.Select(item => new Tuple<int, int>(item.x, item.y)).ToList();

            Bf.Serialize(stream, tupledSlots);
            Bf.Serialize(stream, _favoritedItems.ToList());
        }

        private static object TryDeserialize(Stream stream)
        {
            object result;

            try
            {
                result = Bf.Deserialize(stream);
            }
            catch (SerializationException)
            {
                result = null;
            }

            return result;
        }

        private static void LoadProperty<T>(Stream file, out T property) where T : new()
        {
            object obj = TryDeserialize(file);

            if (obj is T property1)
            {
                property = property1;
                return;
            }

            property = Activator.CreateInstance<T>();
        }

        private void Load()
        {
            using Stream stream = File.Open(_configPath, FileMode.OpenOrCreate);
            stream.Seek(0L, SeekOrigin.Begin);

            _favoritedSlots = new HashSet<Vector2i>();
            _favoritedItems = new HashSet<string>();

            List<Tuple<int, int>>? deserializedFavoritedSlots = new List<Tuple<int, int>>();
            LoadProperty(stream, out deserializedFavoritedSlots);

            foreach (Tuple<int, int>? item in deserializedFavoritedSlots)
            {
                _favoritedSlots.Add(new Vector2i(item.Item1, item.Item2));
            }

            List<string>? deserializedFavoritedItems = new List<string>();
            LoadProperty(stream, out deserializedFavoritedItems);

            foreach (string? item in deserializedFavoritedItems)
            {
                _favoritedItems.Add(item);
            }
        }

        public void ToggleSlotFavoriting(Vector2i position)
        {
            _favoritedSlots.XAdd(position);
            Save();
        }

        public bool ToggleItemNameFavoriting(ItemDrop.ItemData.SharedData item)
        {
            _favoritedItems.XAdd(item.m_name);
            Save();

            return true;
        }

        public bool IsSlotFavorited(Vector2i position)
        {
            return _favoritedSlots.Contains(position);
        }

        public bool IsItemNameFavorited(ItemDrop.ItemData.SharedData item)
        {
            return _favoritedItems.Contains(item.m_name);
        }

        public bool IsItemNameOrSlotFavorited(ItemDrop.ItemData item)
        {
            return IsItemNameFavorited(item.m_shared) || IsSlotFavorited(item.m_gridPos);
        }

        private readonly string _configPath;
        private HashSet<Vector2i> _favoritedSlots;
        private HashSet<string> _favoritedItems;
        private static readonly BinaryFormatter Bf = new BinaryFormatter();
    }

    public static class CollectionExtension
    {
        public static bool XAdd<T>(this HashSet<T> instance, T item)
        {
            if (instance.Contains(item))
            {
                instance.Remove(item);
                return false;
            }
            else
            {
                instance.Add(item);
                return true;
            }
        }
    }
}