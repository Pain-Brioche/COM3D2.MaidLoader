using System;
using System.IO;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.MaidLoader
{
    internal static class AssetManager
    {
        private static ManualLogSource logger = MaidLoader.logger;
        private static Harmony harmony;

        internal static void Init()
        {
            //Harmony stuff
            harmony = Harmony.CreateAndPatchAll(typeof(AssetManager));
        }

        //Adds every .asset_bg found in the Mod folder to the game's dictionary.
        [HarmonyPatch(typeof(GameUty), nameof(GameUty.Init))]
        [HarmonyPostfix]
        public static void InitPostfix()
        {
            //Retrieve any .asset_bg the game found in the Mod folder
            string[] array = GameUty.FileSystemMod.GetFileListAtExtension(".asset_bg");
            for (int i = 0; i < array.Length; i++)
            {
                string fileName = Path.GetFileName(array[i]);
                logger.LogDebug($"Adding asset: {fileName}");

                //Overrides or adds .asset_bg to the background dictionary
                GameUty.BgFiles[fileName] = GameUty.FileSystemMod;
            }
        }

        //Overrides loading from ressource
        [HarmonyPatch(typeof(Resources), nameof(Resources.Load))]
        [HarmonyPatch(new Type[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool LoadPrefix(string path, ref UnityEngine.Object __result)
        {
            string assetName = Path.GetFileName(path).ToLower() + ".asset_bg";

            //Check for .asset_bg instead of loading the original ressource
            if (GameUty.BgFiles.ContainsKey(assetName))
            {
                __result = GameMain.Instance.BgMgr.CreateAssetBundle(assetName);
                return false;
            }
            return true;
        }

    }
}
