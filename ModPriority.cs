using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace COM3D2.MaidLoader
{
    //Look into MaidLoader.QuickMod before GameUty.FileSystemMod before GameUty.FileSystem
    internal class ModPriority
    {
        private static Harmony harmony;

        internal static void Init()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(ModPriority));
        }

        /// <summary>
        /// Checks if the file exists in QuickMod before FileSystemMod before FileSystem.
        /// </summary>
        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.IsExistentFile))]
        [HarmonyPrefix]
        public static bool IsExistentFile_Prefix(string file_name, ref bool __result)
        {
            if (MaidLoader.quickMod != null)
            {
                if (MaidLoader.quickMod.qmFileSystem != null && MaidLoader.quickMod.qmFileSystem.IsExistentFile(file_name))
                {
                    __result = true;
                    return false;
                }
            }

            if (GameUty.FileSystemMod != null && GameUty.FileSystemMod.IsExistentFile(file_name))
            {
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
            if (MaidLoader.quickMod != null)
            {
                if (MaidLoader.quickMod.qmFileSystem != null && MaidLoader.quickMod.qmFileSystem.IsExistentFile(file_name))
                {
                    __result = MaidLoader.quickMod.qmFileSystem.FileOpen(file_name);
                    return false;
                }
            }

            if (GameUty.FileSystemMod != null && GameUty.FileSystemMod.IsExistentFile(file_name))
            {
                __result =  GameUty.FileSystemMod.FileOpen(file_name);
                return false;
            }
            return true;
        }
    }
}
