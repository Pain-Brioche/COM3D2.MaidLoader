using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine.SceneManagement;


namespace COM3D2.MaidLoader
{
    [BepInPlugin("COM3D2.MaidLoader", "Maid Loader", "1.0.0")]
    [BepInDependency("ShortStartLoader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("COM3D2.CornerMessage", BepInDependency.DependencyFlags.SoftDependency)]
    public class MaidLoader : BaseUnityPlugin
    {
        internal static ManualLogSource logger;
        internal static MaidLoader instance;
        internal static QuickModGlobal quickModGlobal;

        internal static bool SSL;

        //Startup options
        internal static ConfigEntry<bool> loadScripts;
        internal static ConfigEntry<bool> loadSounds;
        internal static ConfigEntry<bool> useModOverride;
        internal static ConfigEntry<bool> useCustomModOverride;


        // Quick Load options
        internal static ConfigEntry<bool> useQuickMod;
        internal static ConfigEntry<bool> useGlobal;
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
            useGlobal = Config.Bind("QuickMod", "Use Global QuickMod", true, "Disable to use a dedicated QuickMod folder (A dedicated folder is faster");

            //Advanced
            quickModFolder = Config.Bind("QuickMod Advanced", "QuickMod folder Name", "Mod_QuickMod", new ConfigDescription("Dedicated QuickMod folder name", null, "Advanced"));
            quickModTimer = Config.Bind("QuickMod Advanced", "Seconds before regfresh", 5, new ConfigDescription("How many seconds to wait after last file was added before updating the FileSystem, setting this too low may result in incomplte refresh or ignrored files", new AcceptableValueRange<int>(1, 60), "Advanced"));
            useModOverride = Config.Bind("Startup Advanced", "Enable Mod override", true, new ConfigDescription("Whether or not mods can replace game's assets, DEBUG ONLY!", null, "Advanced"));
            useCustomModOverride = Config.Bind("Startup Advanced", "Enable Custom Mod override", true, new ConfigDescription("Disable to use game's built-in ModPriority, Usefull to disable some unwanted mod behaviour", null, "Advanced"));

            instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;

            //BepinEx loggin
            logger = Logger;

            // Check if ShortStartLoader is loaded
            SSL = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("ShortStartLoader");

            // Check if CornderMessage is loaded.
            CornerMessage.CornerMessageLoaded = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("COM3D2.CornerMessage");

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
                quickModGlobal = new QuickModGlobal();
            }
        }
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
