using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace COM3D2.MaidLoader
{
    public class QuickModFileSystem : FileSystemWindows
    {
        //private new DLLFileSystem.Data data_ = default(DLLFileSystem.Data);


        public QuickModFileSystem()
        {
            Harmony harmony = Harmony.CreateAndPatchAll(typeof(QuickModFileSystem));
        }


        public new bool AddAutoPath(string path)
        {
            return DLLFileSystem.AddAutoPath(ref this.data_, path);
        }

        public new bool AddFolder(string path)
        {
            return DLLFileSystem.AddFolder(ref this.data_, path);
        }

        public new string[] GetFileListAtExtension(string extension)
        {
            return GetFileListAtExtension_Reverse(this, extension);
        }


        [HarmonyPatch]
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(FileSystemWindows), nameof(FileSystemWindows.GetFileListAtExtension))]
        public static string[] GetFileListAtExtension_Reverse(object instance, string extension)
        {
            // its a stub so it has no initial content
            throw new NotImplementedException("It's a stub");
        }
    }

    public class ExperimentalFileSystem
    {

    }
}
