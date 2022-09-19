using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;
using BepInEx;

namespace COM3D2.MaidLoader
{
    internal class QuickMod
    {
        private ManualLogSource logger = MaidLoader.logger;
        private FileSystemWatcher watcher;

        private List<string> addedMenus = new List<string>();
        private static string gamePath = UTY.gameProjectPath;
        private static string quickModFolder = "Mod_QuickLoad";
        private static string quickModPath = Path.Combine(gamePath, quickModFolder);

        private bool rebuildFileSystem = false;

        private Stopwatch waitTimer = new Stopwatch();

        private static Harmony harmony;
        private static Harmony harmony2;

        public static FileSystemWindows qlFileSystem = new();


        internal QuickMod()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(InitPatch));
        }

        private void Start()
        {
            harmony2 = Harmony.CreateAndPatchAll(typeof(FileSystemModPatch));

            UpdateFileSystem();

            InitMenu();

            //Starts a Coroutine to periodically check if items were added.
            MaidLoader.instance.StartCoroutine(CheckNewFileCO());

            Monitor();
        }

        /// <summary>
        /// Monitor changes in QuickMod folder
        /// </summary>
        private void Monitor()
        {
            watcher = new FileSystemWatcher(quickModPath);
            logger.LogInfo($"Monitoring started in {quickModPath}");

            watcher.NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size;


            watcher.Filter = "*.*";

            watcher.Changed += OnFileChanged;
            watcher.Renamed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileDeleted;

            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Triggered on events raised by the monitor
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            string[] validFiles = { ".menu", ".tex", ".model", ".mate", ".pmat" };

            if (validFiles.Contains(Path.GetExtension(e.FullPath)))
            {
                if(!waitTimer.IsRunning)
                    waitTimer.Start();
                else
                {
                    waitTimer.Reset();
                    waitTimer.Start();
                }

                rebuildFileSystem = true;
                logger.LogInfo($"File added: {Path.GetFileName(e.FullPath)}");
            }                            
        }


        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            logger.LogWarning($"COM's FileSystem does NOT support deleting mods!\nAny item using {Path.GetFileName(e.FullPath)} will give an error unless a replacement is found.");
        }

        /// <summary>
        /// Periodically checks if the files system needs to be rebuilt.
        /// Rebuild it if needed and x seconds ellapsed since last trigger.
        /// </summary>
        private IEnumerator CheckNewFileCO()
        {
            for (;;)
            {
                if (rebuildFileSystem)
                {
                    if(waitTimer.ElapsedMilliseconds >= 5000)
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        UpdateFileSystem();
                        InitMenu();
                        sw.Stop();
                        logger.LogInfo($"QM File System updated in {sw.ElapsedMilliseconds}ms");
                        waitTimer.Reset();
                        rebuildFileSystem = false;
                    }
                    else
                    {
                        logger.LogInfo($"Adding new files in {5000 - waitTimer.ElapsedMilliseconds}ms");
                    }
                }
                yield return new WaitForSeconds(1);
            }
        }

        /// <summary>
        /// Build the basic needed for a FileSystemWindows FileSystem.
        /// </summary>
        private void UpdateFileSystem()
        {
            FileSystemWindows oldFS = qlFileSystem;
            // Making a new FileSystem because Dispose() crashes the game... Not ideal.
            qlFileSystem = new();

            logger.LogInfo("Updating QM File System");
            if (Directory.Exists(quickModPath))
            {
                qlFileSystem.SetBaseDirectory(gamePath);
                qlFileSystem.AddFolder(quickModFolder);

                string[] files = qlFileSystem.GetList(string.Empty, AFileSystemBase.ListType.AllFolder);
                foreach (string file in files)
                {
                    if (qlFileSystem.AddAutoPath(file))
                    {
                        //logger.LogMessage($"{file} AddAutoPath");
                    }
                }
            }

            oldFS.Dispose();
        }

        /// <summary>
        /// Do everything needed to add a .menu to the edit mode panels.
        /// </summary>
        private void InitMenu()
        {
            // Get all .menu from QuickMod FileSystem
            List<string> files = qlFileSystem.GetFileListAtExtension("menu").ToList();

            if (files.Count == 0 || files == null)
                return;

            //Remove already added .menu
            foreach (string file in addedMenus)
                files.Remove(file);

            foreach (string menu in files)
                logger.LogInfo($"New menu found: {menu}");


            // Try to find SceneEdit
            SceneEdit sceneEdit = GameObject.Find("__SceneEdit__").GetComponent<SceneEdit>();

            List<SceneEdit.SMenuItem> menuList = new List<SceneEdit.SMenuItem>(files.Count);
            Dictionary<int, List<int>> menuGroupMemberDic = new Dictionary<int, List<int>>();

            // Go through all added .menu and add them to the already existing SceneEdit lists
            foreach (string strFileName in files)
            {
                logger.LogMessage(strFileName);
                SceneEdit.SMenuItem mi = new SceneEdit.SMenuItem();

                // Parse the actual .menu
                if (SceneEdit.GetMenuItemSetUP(mi, strFileName, false))
                {
                    // ignore is this .menu is made for a man or has no icon
                    if (!mi.m_bMan && !(mi.m_texIconRef == null))
                    {
                        //Doesn't look like much, but this is the most important part.
                        sceneEdit.AddMenuItemToList(mi);
                        menuList.Add(mi);

                        //Not sure about this one, 
                        if (!sceneEdit.m_menuRidDic.ContainsKey(mi.m_nMenuFileRID))
                        {
                            sceneEdit.m_menuRidDic.Add(mi.m_nMenuFileRID, mi);
                        }

                        // check for _Zn parents. Currently does nothing as mods aren't stacked anyway.
                        string parentMenuName = SceneEdit.GetParentMenuFileName(mi);
                        if (!string.IsNullOrEmpty(parentMenuName))
                        {
                            int hashCode = parentMenuName.GetHashCode();
                            if (!menuGroupMemberDic.ContainsKey(hashCode))
                            {
                                menuGroupMemberDic.Add(hashCode, new List<int>());
                            }
                            menuGroupMemberDic[hashCode].Add(mi.m_strMenuFileName.ToLower().GetHashCode());
                        }

                        // Check for _set and _del special cases
                        else if (mi.m_strCateName.IndexOf("set_") != -1 && mi.m_strMenuFileName.IndexOf("_del") == -1)
                        {
                            mi.m_bGroupLeader = true;
                            mi.m_listMember = new List<SceneEdit.SMenuItem>();
                            mi.m_listMember.Add(mi);
                        }
                    }
                } 
                addedMenus.Add(strFileName);
            }

            // Deals with .mod and sub menus
            sceneEdit.StartCoroutine(sceneEdit.FixedInitMenu(menuList, sceneEdit.m_menuRidDic, menuGroupMemberDic));
            sceneEdit.StartCoroutine(sceneEdit.CoLoadWait());            
        }       

        
        internal class InitPatch
        {
            // Wait for the edit mode to finish loading entirely before doing anything, this is done so QL doesn't interfere with normal load times.
            [HarmonyPatch(typeof(SceneEdit), nameof(SceneEdit.OnCompleteFadeIn))]
            [HarmonyPostfix]
            internal static void OnCompleteFadeIn_Postfix()
            {
                //MaidLoader.quickMod.Start();
            }
        }
        

        internal class FileSystemModPatch
        {
            // Patch GameUty.FileOpen to also look inside QuickMod FileSystem.
            [HarmonyPatch(typeof(GameUty), nameof(GameUty.FileOpen))]
            [HarmonyPostfix]
            internal static void FileOpen_Postfix(string fileName, ref AFileBase __result)
            {
                if (__result == null)
                {
                    bool exists = qlFileSystem.IsExistentFile(fileName);

                    if (!exists)
                        MaidLoader.logger.LogMessage($"{fileName} doesn't exist");
                    else
                        __result = qlFileSystem.FileOpen(fileName);
                }
            }
        }
    }
}
