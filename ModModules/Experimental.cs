using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace COM3D2.MaidLoader
{    
    public class ExperimentalFileSystem : AFileSystemBase
    {
        public List<ModItemFile> ItemFileDatabase { get; set; } = new List<ModItemFile>();
    
        string baseDirectory = @"Y:\COM3D2 Debug\Mod_QuickMod\";
    
    
    
        public bool AddFolder(string folder)
        {
            string addedPath = baseDirectory + folder + "\\";

            string[] files = Directory.GetFiles(addedPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var item = new ModItemFile(file);
                ItemFileDatabase.Add(item);
            }
            return true;
        }    
    
        public override AFileBase FileOpen(string f_strFileName)
        {
            ModItemFile[] MIF = ItemFileDatabase.Where(n => n.Name == f_strFileName).ToArray();
    
            if (MIF != null && MIF.Length >= 0)
            {
                MaidLoader.logger.LogMessage($"Found {MIF.Length} files corresponding to {f_strFileName}");
    
                foreach (var item in MIF)
                {
                    MaidLoader.logger.LogInfo(item.FullPath);
                }
    
                MaidLoader.logger.LogInfo($"Using: {MIF[0].FullPath}");
    
                return new ExperimentalFileBase(MIF[0].FullPath);
            }
            else
            {
                return new FileWfNull();
            }
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
        public bool AddAutoPath(string str)
        {
            return true;
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
    
    
    public class ExperimentalFileBase : AFileBase
    {
        public string Path { get; set; }
    
        public ExperimentalFileBase(string path)
        {
            Path = path;
        }
    
        public override IntPtr NativePointerToInterfaceFile => throw new NotImplementedException();
    
        public override void Dispose(bool is_release_managed_code)
        {
            if (this.is_disposed_)
                return;
    
            this.is_disposed_ = true;
        }
    
        public override bool IsValid()
        {
            return true;
        }
    
        public override int Read(ref byte[] f_byBuf, int f_nReadSize)
        {
            return 0;
        }
    
        public override byte[] ReadAll()
        {
            return File.ReadAllBytes(Path);
        }
    
    
    
        #region useless stuff
        public override int Seek(int f_unPos, bool absolute_move)
        {
            throw new NotImplementedException();
        }
        public override int Tell()
        {
            throw new NotImplementedException();
        }
        public override int GetSize()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
    
    
    public class ModItemFile
    {
        public string FullPath { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
    
    
    
        public ModItemFile(string path)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
            Extension = Path.GetExtension(Name);
        }
    }
    
}
