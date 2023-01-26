using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using System.Diagnostics;

namespace COM3D2.MaidLoader
{
    internal class PmatManager
    {
        private static Harmony harmony;
        internal static ManualLogSource logger = MaidLoader.logger;

        internal static void Init()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(PmatManager));
        }


        // Original code from Neerhom's ModLoader
        // Patch ReadMaterial to load pmat from the Mod folder, override base.pmat files and handle .pmat hash conflicts.
        [HarmonyPatch(typeof(ImportCM), nameof(ImportCM.ReadMaterial))]
        [HarmonyPrefix]
        private static bool ReadMaterial_Prefix()
        {
            Dictionary<int, KeyValuePair<string, float>> pmatlist = new();
            Dictionary<int, string> DupFilter = new();

            if (ImportCM.m_hashPriorityMaterials == null)
            {

                //Get .pmat from FileSystem and FileSystemMod
                string[] gamepmat = GameUty.FileSystem.GetList("prioritymaterial", AFileSystemBase.ListType.AllFile);
                string[] modpmat = GameUty.FileSystemMod.GetFileListAtExtension(".pmat");

                if (modpmat != null && 0 < modpmat.Length)
                {
                    //Read all .pmat found in the Mod folder
                    for (int i = 0; i < modpmat.Length; i++)
                    {
                        if (Path.GetExtension(modpmat[i]) == ".pmat")
                        {
                            string filename = Path.GetFileName(modpmat[i]);
                            using (AFileBase aFileBase = GameUty.FileSystemMod.FileOpen(filename))
                            {
                                //Check if the file is a valid .pmat
                                if (aFileBase.IsValid())
                                {
                                    byte[] buffer = aFileBase.ReadAll();
                                    using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(buffer), Encoding.UTF8))
                                    {
                                        //Read .pmat info, redudant .pmat are excluded
                                        string a = binaryReader.ReadString();
                                        if (a == "CM3D2_PMATERIAL")
                                        {
                                            int num = binaryReader.ReadInt32();
                                            int key = binaryReader.ReadInt32();
                                            string key2 = binaryReader.ReadString();
                                            float value = binaryReader.ReadSingle();
                                            if (!pmatlist.ContainsKey(key))
                                            {
                                                DupFilter.Add(key, filename);
                                                pmatlist.Add(key, new KeyValuePair<string, float>(key2, value));
                                            }
                                            else
                                            {
                                                logger.LogInfo($"Skipping {filename}  because its target material has already been changed by {DupFilter[key]}");
                                            }
                                        }
                                        else
                                        {
                                            logger.LogInfo($"ヘッダーエラー\n" + filename + "File header of Mod .pmat file is invalid! skipping it!");
                                        }
                                    }
                                }
                                else
                                {
                                    logger.LogInfo(filename + "を開けませんでした ( Mod .pmat file is invalid! skipping it)");
                                }
                            }
                        }
                    }
                }

                // Do the same for official .pmat, 
                if (gamepmat != null && 0 < gamepmat.Length)
                {
                    for (int i = 0; i < gamepmat.Length; i++)
                    {
                        if (Path.GetExtension(gamepmat[i]) == ".pmat")
                        {
                            string text = gamepmat[i];
                            using (AFileBase aFileBase = GameUty.FileSystem.FileOpen(text))
                            {
                                if (aFileBase.IsValid())
                                {
                                    byte[] buffer = aFileBase.ReadAll();
                                    using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(buffer), Encoding.UTF8))
                                    {
                                        string a = binaryReader.ReadString();
                                        if (a == "CM3D2_PMATERIAL")
                                        {
                                            int num = binaryReader.ReadInt32();
                                            int key = binaryReader.ReadInt32();
                                            string key2 = binaryReader.ReadString();
                                            float value = binaryReader.ReadSingle();
                                            if (!pmatlist.ContainsKey(key))
                                            {
                                                pmatlist.Add(key, new KeyValuePair<string, float>(key2, value));
                                            }
                                        }
                                        else
                                        {
                                            logger.LogInfo("ヘッダーエラー\n" + text + "File header of official .pmat file or Mod override is invalid! skipping it!");
                                        }
                                    }
                                }
                                else
                                {
                                    logger.LogInfo(text + "を開けませんでした ( Official .pmat file or Mod override is invalid! skipping it)");
                                }
                            }
                        }
                    }
                }

                ImportCM.m_hashPriorityMaterials = pmatlist;
            }

            return true;
        }
    }
}
