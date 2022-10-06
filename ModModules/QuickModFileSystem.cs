using System;
using System.Collections.Generic;
using System.IO;
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

    public class ExperimentalFileSystem : AFileSystemBase
    {
        public List<ModItemFile> ItemFileDatabase { get; set; }

        string baseDirectory = UTY.gameProjectPath + "\\";



        public bool AddFolder(string folder)
        {
            string addedPath = Path.Combine(baseDirectory, folder);

            string[] files = Directory.GetFiles(addedPath, ".", SearchOption.AllDirectories);

            foreach(var file in files)
            {
                var item = new ModItemFile(file);
                ItemFileDatabase.Add(item);
            }

            return true;
        }


        public override AFileBase FileOpen(string f_strFileName)
        {
            throw new NotImplementedException();
        }

        public override string[] GetFileListAtExtension(string extension)
        {
            string[] files = ItemFileDatabase.Where(e => e.Extension == extension).Select(f => f.Name).ToArray();

            return files;
        }

        public override string[] GetList(string f_str_path, ListType type)
        {
            throw new NotImplementedException();
        }

        public override bool IsExistentFile(string f_strFileName)
        {
            return ItemFileDatabase.Any(e => e.Name == f_strFileName);
        }

        public override void Dispose(bool is_release_managed_code)
        {
            if (this.is_disposed_)
            {
                return;
            }
            this.is_disposed_ = true;
        }
        public void SetBaseDirectory(string directory)
        {
            baseDirectory = Path.Combine(UTY.gameProjectPath, directory);
        }

        #region useless stuff
        public override IntPtr NativePointerToInterfaceFileSystem => throw new NotImplementedException();
        public override IntPtr NativePointerToInterfaceFileSystemWide => throw new NotImplementedException();
        public override void AddAutoPathForAllFolder(bool multiThreadProcessing)
        {
            return;
        }
        public override bool IsFinishedAddAutoPathJob(bool sleepBlockCheck)
        {
            return true;
        }
        public override void ReleaseAddAutoPathJob()
        {
            return;
        }
        public override bool IsValid()
        {
            return true;
        }
        #endregion


    }


    public class ModItemFile
    {
        public string fullPath { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }



        public ModItemFile(string path)
        {
            fullPath = path;
            Name = Path.GetFileName(path);
            Extension = Path.GetExtension(Name);
        }
    }
}
