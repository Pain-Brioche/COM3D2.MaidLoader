using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using COM3D2API;
using System;
using UnityEngine.SceneManagement;


namespace COM3D2.MaidLoader
{
    [BepInPlugin("COM3D2.MaidLoader", "Maid Loader", "1.2.3")]
    [BepInDependency("ShortStartLoader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("COM3D2.CornerMessage", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("deathweasel.com3d2.api", BepInDependency.DependencyFlags.HardDependency)]

    public class MaidLoader : BaseUnityPlugin
    {
        internal static ManualLogSource logger;
        internal static MaidLoader instance;
        internal static ArcManager arcManager;
        public static QuickMod quickMod;
        public static RefreshMod refreshMod;

        internal static bool SSL;

        //Startup options
        internal static ConfigEntry<bool> loadScripts;
        internal static ConfigEntry<bool> loadSounds;
        internal static ConfigEntry<bool> loadArc;
        internal static ConfigEntry<bool> useModOverride;
        internal static ConfigEntry<bool> useCustomModOverride;
        internal static ConfigEntry<bool> useDedicatedSSFolder;

        // QuickMod options
        internal static ConfigEntry<bool> useQuickMod;
        internal static ConfigEntry<string> quickModPath;
        internal static ConfigEntry<int> quickModTimer;
        internal static ConfigEntry<bool> quickModAutoRefresh;


        private void Awake()
        {
            instance = this;

            //BepinEx loggin
            logger = Logger;

            // Load config
            //Startup options
            loadScripts = Config.Bind("General", "Load scripts (.ks)", true, "Whether or not .ks from the Mod folder will be loaded, disabling this can improve loading time.");
            loadSounds = Config.Bind("General", "Load sounds (.ogg)", true, "Whether or not .ogg from the Mod folder will be loaded, disabling this can improve loading time.");
            loadArc = Config.Bind("General", "Load Arc (.arc)", false, "Whether or not .arc from the Mod folder will be loaded, to avoid whenever possible.");

            //QuickMod options
            useQuickMod = Config.Bind("QuickMod", "1. Use QuickMod", false, "Use the Dynamic Mod Loading system");
            quickModPath = Config.Bind("QuickMod", "3. Custom Mod folder", "Mod_QuickMod" , "Dedicated QuickMod folder to monitor");
            quickModAutoRefresh = Config.Bind("QuickMod", "4. Auto refresh", true, "If enabled mods will be automatically refresh x seconds after the last file is added.");
            quickModTimer = Config.Bind("QuickMod", "5. Auto refresh delay", 5, new ConfigDescription("How many seconds to wait after last file was added before updating the FileSystem, setting this too low may result in incomplte refresh or ignored files", new AcceptableValueRange<int>(1, 60), "Advanced"));

            //Advanced
            useModOverride = Config.Bind("Advanced", "Enable Mod override", true, new ConfigDescription("Whether or not mods can replace game's assets, DEBUG ONLY!", null, "Advanced"));
            useCustomModOverride = Config.Bind("Advanced", "Enable Custom Mod override", true, new ConfigDescription("Disable to use game's built-in ModPriority, Usefull to disable some unwanted mod behaviour", null, "Advanced"));
            useDedicatedSSFolder = Config.Bind("Advanced", "Use a specific folder for Scripts and Sounds", false, new ConfigDescription("Use a specific folder to look Scripts and Sounds into", null, "Advanced"));


            SceneManager.sceneLoaded += OnSceneLoaded;

            // Check if ShortStartLoader is loaded
            SSL = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("ShortStartLoader");

            // Check if CornderMessage is loaded.
            CornerMessage.CornerMessageLoaded = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("COM3D2.CornerMessage");

            // Add gear menu button
            SystemShortcutAPI.AddButton("RefreshMod", () =>
            {
                if(refreshMod != null)
                    MaidLoader.instance.StartCoroutine(refreshMod.RefreshCo());

            }, "Refresh Mod Folder", Convert.FromBase64String(icon));

            if (useQuickMod.Value)
            {
                SystemShortcutAPI.AddButton("QuickMod", () =>
                {
                    if (quickMod != null)
                        MaidLoader.instance.StartCoroutine(quickMod.RefreshCo());

                }, "Refresh QuickMod", Convert.FromBase64String(quickModRefreshIcon));
            }

            //Only load dummy .arc when needed
            if (loadScripts.Value || loadSounds.Value || loadArc.Value)
            {
                arcManager = new();
            }    

            if (useModOverride.Value)
            {
                //Whether to use ModLoader style override or the game's own method.
                if (useCustomModOverride.Value)
                    ModPriority.Init();
                else
                    GameUty.ModPriorityToModFolder = true;
            }


            //Load .asset_bg found in the Mod folder.
            AssetManager.Init();

            //Load modded Man bodies and heads
            ManManager.Init();

            //Load modded .pmat
            PmatManager.Init();

            //Load some custom .nei
            NeiManager.Init();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "SceneTitle")
            {
                if(refreshMod == null)
                    refreshMod = new RefreshMod();

                if (useQuickMod.Value && quickMod == null)
                    quickMod = new QuickMod();
            }
        }

        private static readonly string icon = "iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAYAAAByDd+UAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEh" +
            "ZcwAADsMAAA7DAcdvqGQAAANRSURBVEhLtVZdL2NRFF1FQpsiOmglgiA0fRIkTXjzNsb3TPso/oX/4Nlf8GA0KmTGkycRwpD4mPqOF4miJb46EXRmr3NvxW17TZvo" +
            "Snbce7budc9ee+9zLADsYuViTrFPYlaxPLGPQFwsJnYlFhaLiqG2paXFt7W19ftvjrC8vPyLHMJVR0IvF3RfzsANkYsp/SzvP8mca1gsli8k/CqEAW0pFeLDy8sLL" +
            "i8vsba2hoODA1xdURKgrKwMzc3NaG9vR2VlJfLz8xlU+dJBfN/o9UnQ79qSESRj8KWlJayvr6OiogJutxvl5awxIBKJYH9/HxcXF2hra0NnZyccDocpqaz73yVkwL" +
            "m5OcRiMXR1dand5OUZCzgej6tdLywswGazoaenR31YOpDQtPxvb28xPT2tAvp8PrWzZDKCa/T5/X71zt/c3Nyo53QwJaRe19fX6OvrU/q8pw3BXfX29uLu7g6Li4t" +
            "4fn7WPZo0CZgSlpaWYmhoyDQ96UBtBwYGsLOzg7OzM33VCFNCaVQ0Njbqb5mjvr4eVVVV2NvbM+wsAVPCgoKC/6YxGWwfEnk8Hpyeniotj46ODHFSCPmj+/t7gwaZ" +
            "4vHxEZOTk9je3sbDwwNmZmYwPz+vezUYCFmR/OexsTHVd9nCarVieHgYm5ubKs7h4SH6+/t1rwYDIdMwNTWF8/NzTExMYHd3N60OZmDqGhoaMDIyApfLpdqpurpa9" +
            "2owNP74+DiKi4tVadvtdhwfH2N0dFQ9ZwPKwfQWFRWpcZcAG/+VkDs5OTlRpR0MBtHU1KRmJSuVBfQRIKEhpSxp7rCmpkalk6TZkvHDn56e9LdUvBImSpd/OTM5kD" +
            "kjswVl2NjYMNU+pS0Ip9OpGp9lzQLKFDzCKEfWs5RCd3R0qLE2OzuLcDj8brXSx4zwZOFI9Hq9rxlLRlpCoqSkRM3FwsJCBAIBhEIhNRSSwd6l3mwnkgwODipSMxj" +
            "aIhn8cp4YKysrWF1dVVX79gCORqPqAOa5yVOfWcnkAM7oisHgnD4sJD4TDM5Kbm1tVenP9IrRLUF/aEu5hRB2U8OozL2QtpQ7iCwczur2VcdLai7vpryT6hfhWqaU" +
            "g5JXfJeYQ8wmZlq9WYJX/T9iFF0aGpF/xkebiKWAI/AAAAAASUVORK5CYII=";

        private static readonly string quickModRefreshIcon = "iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAYAAAByDd+UAAAAAXNSR0IArs4c6QAAAARnQU1BAACx" +
            "jwv8YQUAAAAJcEhZcwAADsIAAA7CARUoSoAAAAQxSURBVEhLtVbXSmRLFF0GzDkr6gyKAUURFUz4Ivpw+xqZ274Ioj/hR/gbBjAgolx9EJ9UFHPOAUHF1Io5e6fW7" +
            "j5Nd9tnmIHbCzbnnKpTtXaucgMQoCRCSbSScCW+StyV/B/4VPKo5ErJqRKTEnzLyckxLi8vr/3nIkxOTs6SQ3F9J2EBByxzLgMNIhdd+pf6HiKzq+Hm5vY3CX8owl" +
            "7z0FeoOXx8fODi4gIzMzPY3t7G1RVDAoSGhiItLQ35+fmIioqCh4cHN5U5Z1Bz/3DWqDbtNg/Zg2TcfGJiAnNzc4iMjER6ejoiIphjwOXlJba2tnB+fo68vDyUlJQ" +
            "gLCxMl1SN1/+SkBsODg7i8fERZWVlYo27u30Cf35+itWjo6Pw8/NDZWWlKOYMJNRN/9vbW/T19cmGRqNRLHMkIzjGufr6evnmmpubG3l3Bl1Cxuv6+hrV1dUSH81N" +
            "Ly8vMJlMMvf29iZjBK2qqqrC3d0dxsbG8P7+bpkxh0aDrktnZ2cRHByMlJQU+aZ7FxYWcHh4KBtQAR8fH5nPzMxEQAD7B7C7u4ve3l40NDQgISFBxrT/fxlDaqhlH" +
            "a2Znp4W67h5fHw8Xl9fJWH29/cRGxuL0tJSWccQtLW1IS4uDuXl5bLellDXpZ6envITcXx8LKVBsuTkZHh7eyMwMBDZ2dmSSGdnZ0LMfzY3N5GRkYGjoyOJJS3W9i" +
            "G+EHLR/f29NQbUjqXh6+srKW8LLy8vhIeHS3YydvRAV1cXVlZW8PDwgP7+fgwPD1v+NsOOkO7gz62trVJ3BEuC7mO8aJkjtMzlf1SqsbERS0tLss/Ozg5qampkXoM" +
            "dId3Q09MjLuro6MDGxoYoQSv5pOjBEiNxeVNTE2JiYqScGG9b2BEODAwgNTUVWVlZyM3NRWdnp7iW8aS7aKkjGAIqRLdq4PqWlhZ5Otau9YuLDAYD6urqJCGYDExt" +
            "vgcFBYnLHAv66elJ2hoV0dodQQX9/f0lyx1hR5+UlCQEiYmJ4k5ay8WsJ2q6urqKvb09IWcn4jezMjo6WsqAoOK2DcERVkItdfmkddScPZJgpyksLERISAjGx8fR3" +
            "t6O7u5ua4xPTk6kLAgqND8/L8TO4LTwGZeRkREsLi6iublZLNDAuefnZ7GYWUlL19bWcHBwIArRanW6o6Kiwq7+CPXtvPDp++LiYumPTKTT01OrxpxjfEhGML60vq" +
            "ioSCwlaUFBwRcyDU4JCW5UW1srtcfeuL6+LtY5gi6la4eGhkQZJh17sB6culQDrWIfnZqakl7KE972AOapwX7Kxs5Tn175nQP4t64Y3Jzdh4nEd4KbM5NZs3S/1uz" +
            "1oObkimFQm/5rHnItFKGBMTSpvrduHnIdVFjYnOX29Z2XVFfeTXkntVyEv9GlPKp5xY9RwvOHTVE3e/8Q7PZPShj0MwCXPwFsvTyZS5rnwgAAAABJRU5ErkJggg==";

    }

    // CornerMessage support the way it's intended to be used.
    internal static class CornerMessage
    {
        internal static bool CornerMessageLoaded = false;

        internal static void DisplayMessage(string mess, float dur = 6f)
        {
            if (CornerMessageLoaded)
            {
                TryCornerMessage.DisplayMessage(mess, dur);
            }
        }
        internal static class TryCornerMessage
        {
            internal static void DisplayMessage(string mess, float dur) => COM3D2.CornerMessage.Main.DisplayMessage(mess, dur);
        }
    }
}
