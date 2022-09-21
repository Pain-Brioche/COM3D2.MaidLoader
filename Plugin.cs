using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using COM3D2API;
using System.Drawing;
using System;
using UnityEngine.SceneManagement;


namespace COM3D2.MaidLoader
{
    [BepInPlugin("COM3D2.MaidLoader", "Maid Loader", "1.0.0")]
    [BepInDependency("ShortStartLoader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("COM3D2.CornerMessage", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("deathweasel.com3d2.api", BepInDependency.DependencyFlags.HardDependency)]

    public class MaidLoader : BaseUnityPlugin
    {
        internal static ManualLogSource logger;
        internal static MaidLoader instance;
        public static QuickMod quickMod;

        internal static bool SSL;

        //Startup options
        internal static ConfigEntry<bool> loadScripts;
        internal static ConfigEntry<bool> loadSounds;
        internal static ConfigEntry<bool> useModOverride;
        internal static ConfigEntry<bool> useCustomModOverride;
        internal static ConfigEntry<bool> useDedicatedSSFolder;

        // Quick Load options
        internal static ConfigEntry<bool> useQuickMod;
        internal static ConfigEntry<bool> useModFolder;
        internal static ConfigEntry<string> quickModFolder;
        internal static ConfigEntry<int> quickModTimer;


        private void Awake()
        {
            // Load config
            //Startup options
            loadScripts = Config.Bind("Startup options", "Load scripts (.ks)", true, "Whether or not .ks from the Mod folder will be loaded, disabling this can improve loading time.");
            loadSounds = Config.Bind("Startup options", "Load sounds (.ogg)", true, "Whether or not .ogg from the Mod folder will be loaded, disabling this can improve loading time.");


            //QuickMod options
            useQuickMod = Config.Bind("QuickMod", "Use QuickMod", true, "Use the Dynamic Mod Loading system");
            useModFolder = Config.Bind("QuickMod", "Use standard Mod folder for QuickMod", false, "Disable to use a dedicated QuickMod folder (A dedicated folder is faster");

            //Advanced
            quickModFolder = Config.Bind("QuickMod Advanced", "QuickMod folder Name", "Mod_QuickMod", new ConfigDescription("Dedicated QuickMod folder name", null, "Advanced"));
            quickModTimer = Config.Bind("QuickMod Advanced", "Seconds before regfresh", 5, new ConfigDescription("How many seconds to wait after last file was added before updating the FileSystem, setting this too low may result in incomplte refresh or ignored files", new AcceptableValueRange<int>(1, 60), "Advanced"));
            useModOverride = Config.Bind("Startup Advanced", "Enable Mod override", true, new ConfigDescription("Whether or not mods can replace game's assets, DEBUG ONLY!", null, "Advanced"));
            useCustomModOverride = Config.Bind("Startup Advanced", "Enable Custom Mod override", true, new ConfigDescription("Disable to use game's built-in ModPriority, Usefull to disable some unwanted mod behaviour", null, "Advanced"));
            useDedicatedSSFolder = Config.Bind("Startup Advanced", "Use a specific folder for Scripts and Sounds", false, new ConfigDescription("Use a specific folder to look Scripts and Sounds into", null, "Advanced"));

            instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;

            //BepinEx loggin
            logger = Logger;

            // Check if ShortStartLoader is loaded
            SSL = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("ShortStartLoader");

            // Check if CornderMessage is loaded.
            CornerMessage.CornerMessageLoaded = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("COM3D2.CornerMessage");

            // Add gear menu button
            SystemShortcutAPI.AddButton("QuickMod", () =>
            {

                if (quickMod != null)
                    quickMod.Refresh();

            }, "Refresh new mods", Convert.FromBase64String(icon));

            //Only load dummy .arc when needed
            if (loadScripts.Value || loadSounds.Value)
                ArcManager.Init();         

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

            //An attempt at loading .ogg directly from the mod folder, while it works, this approch is heavier than the dummy .arc and more likely to break.
            //SoundManager.Init();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.buildIndex == 5 && useQuickMod.Value)
            {
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
