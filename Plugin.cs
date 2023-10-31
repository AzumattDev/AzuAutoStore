using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AzuAutoStore.Patches;
using AzuAutoStore.Patches.Favoriting;
using AzuAutoStore.Util;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace AzuAutoStore
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("Azumatt.AzuExtendedPlayerInventory", BepInDependency.DependencyFlags.SoftDependency)]
    public class AzuAutoStorePlugin : BaseUnityPlugin
    {
        internal const string ModName = "AzuAutoStore";
        internal const string ModVersion = "2.1.0";
        internal const string Author = "Azumatt";
        internal const string ModGUID = $"{Author}.{ModName}";
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource AzuAutoStoreLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        internal static readonly string yamlFileName = $"{ModGUID}.yml";
        internal static readonly string yamlPath = Paths.ConfigPath + Path.DirectorySeparatorChar + yamlFileName;
        internal static readonly CustomSyncedValue<string> CraftyContainerData = new(ConfigSync, "azuautostoreData", "");
        internal static readonly CustomSyncedValue<string> CraftyContainerGroupsData = new(ConfigSync, "azuautostoreGroupsData", "");

        //
        internal static Dictionary<string, object> yamlData;
        internal static Dictionary<string, HashSet<string>> groups;
        internal static AzuAutoStorePlugin self;

        public enum Toggle
        {
            Off,
            On
        }

        public void Awake()
        {
            self = this;

            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                new ConfigDescription("If on, the configuration is locked and can be changed by server admins only.", null, new ConfigurationManagerAttributes() { Order = 8 }));
            ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            MustHaveExistingItemToPull = config("1 - General", "Must Have Existing Item To Pull", Toggle.On,
                new ConfigDescription("If on, the chest must already have the item in its inventory to pull it from the world or player into the chest.", null, new ConfigurationManagerAttributes() { Order = 7 }));
            PlayerRange = config("1 - General", "Player Range", 5f,
                new ConfigDescription(
                    "The maximum distance from the player to store items in chests when the Store Shortcut is pressed. Follows storage rules for allowed items.",
                    new AcceptableValueRange<float>(1f, 100f), new ConfigurationManagerAttributes() { Order = 6 }));
            FallbackRange = config("1 - General", "Fallback Range", 10f, new ConfigDescription("The range to use if the container has no range set in the yml file. This will be the fallback range for all containers.", null, new ConfigurationManagerAttributes() { Order = 5 }));
            PlayerIgnoreHotbar = config("1 - General", "Player Ignore Hotbar", Toggle.On,
                new ConfigDescription("If on, the player's hotbar will not be stored when the Store Shortcut is pressed.", null, new ConfigurationManagerAttributes() { Order = 4 }), false);
            PlayerIgnoreQuickSlots = config("1 - General", "Player Ignore Quick Slots", Toggle.Off,
                new ConfigDescription("If on, the player's quick slots will not be stored when the Store Shortcut is pressed. (Requires Quick Slots mod, turn on only if you need it!)", null, new ConfigurationManagerAttributes() { Order = 3 }), false);
            PingVfxString = TextEntryConfig("1 - General", "Ping VFX", "vfx_Potion_health_medium",
                new ConfigDescription("The VFX to play when a chest is pinged. Leave blank to disable and only highlight the chest. (Full prefab list: https://valheim-modding.github.io/Jotunn/data/prefabs/prefab-list.html)", null, new ConfigurationManagerAttributes() { Order = 2 }));
            HighlightContainers = config("1 - General", "Highlight Containers", Toggle.On,
                new ConfigDescription("If on, the containers will be highlighted when something is stored in them. If off, the containers will not be highlighted if something is stored in them.", null, new ConfigurationManagerAttributes() { Order = 1 }), false);

            SecondsToWaitBeforeStoring = config("1 - General", "Seconds To Wait Before Storing", 10,
                new ConfigDescription("The number of seconds to wait before storing items into chests nearby automatically after you have pressed your hotkey to pause.", new AcceptableValueRange<int>(0, 60)));

            _storeShortcut = config("2 - Shortcuts", "Store Shortcut", new KeyboardShortcut(KeyCode.Period),
                new ConfigDescription("Keyboard shortcut/Hotkey to store your inventory into nearby containers.", null, new ConfigurationManagerAttributes() { Order = 1 }), false);

            _pauseShortcut = config("2 - Shortcuts", "Pause Shortcut", new KeyboardShortcut(KeyCode.Period, KeyCode.LeftShift),
                "Keyboard shortcut/Hotkey to temporarily stop storing items into chests nearby automatically. Does not override the player hotkey store.", false);

            var sectionName = "3 - Favoriting";
            string favoritingKey = $"While holding this, left clicking on items or right clicking on slots favorites them, disallowing storing";

            BorderColorFavoritedItem = config(sectionName, nameof(BorderColorFavoritedItem), new Color(1f, 0.8482759f, 0f), "Color of the border for slots containing favorited items.", false);
            BorderColorFavoritedItem.SettingChanged += (a, b) => FavoritingMode.RefreshDisplay();

            // dark-ish green
            BorderColorFavoritedItemOnFavoritedSlot = config(sectionName, nameof(BorderColorFavoritedItemOnFavoritedSlot), new Color(0.5f, 0.67413795f, 0.5f), "Color of the border of a favorited slot that also contains a favorited item.", false);

            // light-ish blue
            BorderColorFavoritedSlot = config(sectionName, nameof(BorderColorFavoritedSlot), new Color(0f, 0.5f, 1f), "Color of the border for favorited slots.", false);

            DisplayTooltipHint = config(sectionName, nameof(DisplayTooltipHint), true, "Whether to add additional info the item tooltip of a favorited or trash flagged item.", false);

            FavoritingModifierKeybind1 = config(sectionName, nameof(FavoritingModifierKeybind1), new KeyboardShortcut(KeyCode.LeftAlt), $"{favoritingKey} Identical to {nameof(FavoritingModifierKeybind2)}.", false);
            FavoritingModifierKeybind2 = config(sectionName, nameof(FavoritingModifierKeybind2), new KeyboardShortcut(KeyCode.RightAlt), $"{favoritingKey} Identical to {nameof(FavoritingModifierKeybind1)}.", false);
            FavoritedItemTooltip = config(sectionName, nameof(FavoritedItemTooltip), "Item is favorited and won't be stored", string.Empty, false);
            FavoritedSlotTooltip = config(sectionName, nameof(FavoritedSlotTooltip), "Slot is favorited and won't be stored", string.Empty, false);
            ItemOnFavoritedSlotTooltip = config(sectionName, nameof(ItemOnFavoritedSlotTooltip), "Item & Slot are favorited and won't be stored", string.Empty, false);

            if (!File.Exists(yamlPath))
            {
                WriteConfigFileFromResource(yamlPath);
            }

            CraftyContainerData.ValueChanged += OnValChangedUpdate; // check for file changes
            CraftyContainerData.AssignLocalValue(File.ReadAllText(yamlPath));

            AutoDoc();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public void Start()
        {
            BorderRenderer.border = loadSprite("border.png");
        }

        private void AutoDoc()
        {
#if DEBUG
            // Store Regex to get all characters after a [
            Regex regex = new(@"\[(.*?)\]");

            // Strip using the regex above from Config[x].Description.Description
            string Strip(string x) => regex.Match(x).Groups[1].Value;
            StringBuilder sb = new();
            string lastSection = "";
            foreach (ConfigDefinition x in Config.Keys)
            {
                // skip first line
                if (x.Section != lastSection)
                {
                    lastSection = x.Section;
                    sb.Append($"{Environment.NewLine}`{x.Section}`{Environment.NewLine}");
                }

                sb.Append($"\n{x.Key} [{Strip(Config[x].Description.Description)}]" +
                          $"{Environment.NewLine}   * {Config[x].Description.Description.Replace("[Synced with Server]", "").Replace("[Not Synced with Server]", "")}" +
                          $"{Environment.NewLine}     * Default Value: {Config[x].GetSerializedValue()}{Environment.NewLine}");
            }

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, $"{ModName}_AutoDoc.md"), sb.ToString());
#endif
        }

        private static void WriteConfigFileFromResource(string configFilePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = "AzuAutoStore.Example.yml";

            using Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                throw new FileNotFoundException($"Resource '{resourceName}' not found in the assembly.");
            }

            using StreamReader reader = new StreamReader(resourceStream);
            string contents = reader.ReadToEnd();

            File.WriteAllText(configFilePath, contents);
        }

        private void Update()
        {
            if (Player.m_localPlayer == null) return;
            if (_storeShortcut.Value.IsDown() && Player.m_localPlayer.TakeInput())
            {
#if DEBUG
                AzuAutoStoreLogger.LogError("Taking input");
#endif
                Functions.TryStore();
            }

            if (_pauseShortcut.Value.IsDown() && Player.m_localPlayer.TakeInput())
            {
                Boxes.StoringPaused = !Boxes.StoringPaused;
                foreach (Container container in Boxes.Containers)
                {
                    if (!container.m_nview.IsValid()) continue;
                    container.m_nview.InvokeRPC("RequestPause", Boxes.StoringPaused);
                }
            }
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;

            FileSystemWatcher yamlwatcher = new(Paths.ConfigPath, yamlFileName);
            yamlwatcher.Changed += ReadYamlFiles;
            yamlwatcher.Created += ReadYamlFiles;
            yamlwatcher.Renamed += ReadYamlFiles;
            yamlwatcher.IncludeSubdirectories = true;
            yamlwatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            yamlwatcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                Functions.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                Functions.LogError($"There was an issue loading your {ConfigFileName}");
                Functions.LogError("Please check your config entries for spelling and format!");
            }
        }

        private void ReadYamlFiles(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(yamlPath)) return;
            try
            {
                AzuAutoStoreLogger.LogDebug("ReadConfigValues called");
                CraftyContainerData.AssignLocalValue(File.ReadAllText(yamlPath));
            }
            catch
            {
                AzuAutoStoreLogger.LogError($"There was an issue loading your {yamlFileName}");
                AzuAutoStoreLogger.LogError("Please check your entries for spelling and format!");
            }
        }

        private static void OnValChangedUpdate()
        {
            AzuAutoStoreLogger.LogDebug("OnValChanged called");
            try
            {
                YamlUtils.ReadYaml(CraftyContainerData.Value);
                YamlUtils.ParseGroups();
            }
            catch (Exception e)
            {
                AzuAutoStoreLogger.LogError($"Failed to deserialize {yamlFileName}: {e}");
            }
        }

        private static byte[] ReadEmbeddedFileBytes(string name)
        {
            using MemoryStream stream = new();
            Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + name)!.CopyTo(stream);
            return stream.ToArray();
        }

        private static Texture2D loadTexture(string name)
        {
            Texture2D texture = new(0, 0);

            texture.LoadImage(ReadEmbeddedFileBytes("assets." + name));


            return texture!;
        }

        internal static Sprite loadSprite(string name)
        {
            Texture2D texture = loadTexture(name);
            if (texture != null)
            {
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
            }

            return null!;
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Toggle> MustHaveExistingItemToPull = null!;
        private static ConfigEntry<KeyboardShortcut> _storeShortcut = null!;
        private static ConfigEntry<KeyboardShortcut> _pauseShortcut = null!;
        internal static ConfigEntry<int> SecondsToWaitBeforeStoring = null!;
        internal static ConfigEntry<float> PlayerRange = null!;
        internal static ConfigEntry<float> FallbackRange = null!;
        internal static ConfigEntry<Toggle> PlayerIgnoreHotbar = null!;
        internal static ConfigEntry<Toggle> PlayerIgnoreQuickSlots = null!;
        internal static ConfigEntry<string> PingVfxString = null!;
        internal static ConfigEntry<Toggle> HighlightContainers = null!;

        // Favoriting

        public static ConfigEntry<Color> BorderColorFavoritedItem;
        public static ConfigEntry<Color> BorderColorFavoritedItemOnFavoritedSlot;
        public static ConfigEntry<Color> BorderColorFavoritedSlot;
        public static ConfigEntry<bool> DisplayTooltipHint;
        public static ConfigEntry<KeyboardShortcut> FavoritingModifierKeybind1;
        public static ConfigEntry<KeyboardShortcut> FavoritingModifierKeybind2;

        public static ConfigEntry<string> FavoritedItemTooltip;
        public static ConfigEntry<string> FavoritedSlotTooltip;
        public static ConfigEntry<string> ItemOnFavoritedSlotTooltip;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc,
            bool synchronizedSetting = true)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = Functions.TextAreaDrawer
            };
            return TextEntryConfig(group, name, value, new ConfigDescription(desc, null, attributes), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion
    }

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }
    }
}