using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using System.Diagnostics;
using System.Threading;
using System;

namespace COM3D2.MaidLoader
{
    //Look into MaidLoader.QuickMod before GameUty.FileSystemMod before GameUty.FileSystem
    internal static class ModPriority
    {
        private static ManualLogSource logger = MaidLoader.logger;
        private static Harmony harmony;
        private static Dictionary<string,string> IsExistentFileCache = new();
        private static Dictionary<string,string> IsExistentFileQuickModCache = new();
        private static EventWaitHandle ewh;
        private static Task buildCacheTask;

        internal static void Init()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(ModPriority));
            buildCacheTask = Task.Factory.StartNew(BuildModCache);
        }

        internal static void BuildModCache()
        {
            Stopwatch sw = Stopwatch.StartNew();

            //Start a new EventWaitHandle
            ewh = new(false, EventResetMode.ManualReset);

            logger.LogInfo("Building Mod cache");
            string modFolder = UTY.gameProjectPath + "\\Mod";

            if(Directory.Exists(modFolder))
            {
                // Using dictionary as it is faster to search into.
                IsExistentFileCache = Directory.GetFiles(modFolder, "*.*", SearchOption.AllDirectories).Select(x => Path.GetFileName(x).ToLower()).Distinct().ToDictionary(x => x);

                sw.Stop();
                logger.LogInfo($"Mod Cache built in: {sw.ElapsedMilliseconds}ms");
                logger.LogInfo($"{IsExistentFileCache.Count} Entries.");

                //Release EventWaitHandle
                ewh.Set();
            }
        }

        internal static void BuildQuickModCache()
        {
            logger.LogInfo("Building QuickMod Cache");
            string quickModFolder = MaidLoader.quickMod.GetQuickModFolderPath();

            IsExistentFileQuickModCache = Directory.GetFiles(quickModFolder, "*.*", SearchOption.AllDirectories).Select(Path.GetFileName).Distinct().ToDictionary(x => x.ToLower(), x => x);
        }


        /// <summary>
        /// Wait for the cache to be build before making any request to it.
        /// </summary>
        [HarmonyPatch(typeof(Product), nameof(Product.Initialize))]
        [HarmonyPrefix]
        public static bool Initialize_Prefix()
        {
            while(!buildCacheTask.IsCompleted)
            {
                logger.LogInfo("Waiting for MaidLoader's cache to be built");
                // Wait for EvenWaitHandle signal
                ewh.WaitOne();
            }

            return true;
        }



        /// <summary>
        /// Checks if the file exists in QuickMod before FileSystemMod before FileSystem.
        /// </summary>
        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.IsExistentFile))]
        [HarmonyPrefix]
        public static bool IsExistentFile_Prefix(string file_name, ref bool __result)
        {
            string file = file_name.ToLower();
            if (IsExistentFileQuickModCache.ContainsKey(file) || IsExistentFileCache.ContainsKey(file))
            {
                //logger.LogMessage($"{file} found");
                __result = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Load a file by name from QuickMod before FileSystemMod before FileSystem.
        /// </summary>
        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.FileOpen))]
        [HarmonyPrefix]
        public static bool FileOpen_Prefix(string file_name, ref AFileBase __result)
        {
            string file = file_name.ToLower();
            if (MaidLoader.useQuickMod.Value && IsExistentFileQuickModCache.ContainsKey(file))
            {
                //logger.LogMessage($"{file} opened from QuickMod folder");
                __result = MaidLoader.quickMod.qmFileSystem.FileOpen(file);
                return false;                
            }

            if (GameUty.FileSystemMod != null && IsExistentFileCache.ContainsKey(file))
            {
                //logger.LogMessage($"{file} opened from Mod folder");
                __result =  GameUty.FileSystemMod.FileOpen(file_name);
                return false;
            }
            return true;
        }
    }
}
