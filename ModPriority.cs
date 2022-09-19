using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace COM3D2.MaidLoader
{
    //Look into GameUty.FileSystemMod before GameUty.FileSystem
    //Sadly while this works for many
    internal class ModPriority
    {
        private static Harmony harmony;

        internal static void Init()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(ModPriority));
        }

        /// <summary>
        /// Checks if the file exists in FileSystemMod before FileSystem.
        /// </summary>
        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.IsExistentFile))]
        [HarmonyPrefix]
        public static bool IsExistentFile_Prefix(string file_name, ref bool __result)
        {
            if (GameUty.FileSystemMod != null && GameUty.FileSystemMod.IsExistentFile(file_name))
            {
                __result = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Load a file by name from FileSystemMod before FileSystem.
        /// </summary>
        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.FileOpen))]
        [HarmonyPrefix]
        public static bool FileOpen_Prefix(string file_name, ref AFileBase __result)
        {
            if (GameUty.FileSystemMod != null && GameUty.FileSystemMod.IsExistentFile(file_name))
            {
                __result =  GameUty.FileSystemMod.FileOpen(file_name);
                return false;
            }
            return true;
        }

        /*
        /// <summary>
        /// Again Mods Take priority but handled differently
        /// </summary>
        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.GetList))]
        [HarmonyPostfix]
        public static void GetFile_postfix(string f_str_path, AFileSystemBase.ListType type, ref string[] __result)
        {
            if GameUty.FileSystemMod != null)
            {
                //retrieve the list from FileSystemMod and remove duplicates
                List<string> list = GameUty.FileSystemMod.GetList(f_str_path, type).Distinct().ToList();

                foreach (string str in __result)
                {
                    if (!list.Contains(str))
                    {
                        list.Add(str);
                    }
                }
                __result = list.ToArray();
            }
        }
        */
    }
}
