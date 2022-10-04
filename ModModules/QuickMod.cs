using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;
using System.Threading.Tasks;
using BepInEx;

namespace COM3D2.MaidLoader
{
    public class QuickMod
    {
        private ManualLogSource logger = MaidLoader.logger;
        private FileSystemWatcher watcher;

        private List<string> addedMenus = new();
        private List<string> updatedPath = new();

        private bool useModFolder = MaidLoader.useModFolder.Value;
        private bool useAutoRefresh = MaidLoader.quickModAutoRefresh.Value;
        private bool isNeedRefresh = false;

        private static string gamePath = UTY.gameProjectPath.Replace('/', '\\');
        private static string modPath = Path.Combine(gamePath, "Mod");
        private static string customModPath = MaidLoader.quickModPath.Value;
        private static string quickModFolderPath;

        private static readonly string[] validFiles = { ".menu", ".tex", ".model", ".mate", "*.psk", "*.phy", "*.anm" };

        private int waitTimer = MaidLoader.quickModTimer.Value;

        private static Harmony harmony;
        //private static Harmony harmony2;

        public QuickModFileSystem qmFileSystem = new();


        internal QuickMod()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(InitPatch));
        }

        private void Start()
        {
            logger.LogInfo("Starting QuickMod");
            //harmony2 = Harmony.CreateAndPatchAll(typeof(FileSystemModPatch));

            quickModFolderPath = GetQuickModFolderPath();
            logger.LogInfo($"QuickMod folder: {quickModFolderPath}");


            if (quickModFolderPath.Contains(modPath))
            {
                //Get all registered files that already existed at game launch to avoid doubles
                List<string> menus = GameUty.FileSystemMod.GetFileListAtExtension("menu").ToList();
                foreach (string menu in menus)
                    addedMenus.Add(Path.GetFileName(menu));
            }
            else
            {
                //Gather items already in QuickMod's folder if standard mod load isn't used.
                Refresh();
            }

            // Starts folder monitoring as a separate thread
            Task monitor = Task.Factory.StartNew(() =>
            {
                Monitor();
            });

            //Starts a Coroutine to periodically check if files were added.
            if (useAutoRefresh)
                MaidLoader.instance.StartCoroutine(RefreshCO());
          
        }

        /// <summary>
        /// Returns QuickMod folder complete path depending on settings.
        /// If all of custom options fail default back to the classic Mod folder.
        /// </summary>
        private string GetQuickModFolderPath()
        {
            string path = modPath;
              
            if (!useModFolder)
            {
                if (string.IsNullOrEmpty(customModPath))
                    logger.LogWarning("Custom Mod Path isn't properly configured, please correct it in MaidLoader's config. \n\t\t  Standard mod folder will be used instead.");

                else if (!customModPath.Contains(@"\"))
                    path = Path.Combine(gamePath, customModPath);

                else
                    path = customModPath;


                if (!Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch (IOException e)
                    {
                        logger.LogError("QuickMod folder couldn't be found or created!\n\t\t  Standard mod folder will be used instead." + e.Message);
                        path = modPath;
                    }
                }
            }

            return path;
        }

        /// <summary>
        /// Monitor changes in the Mod folder
        /// </summary>
        private void Monitor()
        {
            watcher = new FileSystemWatcher(quickModFolderPath);
            logger.LogInfo($"Monitoring started in {quickModFolderPath}");

            watcher.NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.CreationTime;


            watcher.Filter = "*.*";

            watcher.Changed += OnFileChanged;
            watcher.Renamed += OnFileChanged;
            watcher.Created += OnFileChanged;
            //watcher.Deleted += OnFileDeleted;

            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Triggered on events raised by the monitor
        /// Checks if the files added are relevant to mods and them to the refresh queue
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (validFiles.Contains(Path.GetExtension(e.FullPath)))
            {
                string path = Path.GetDirectoryName(e.FullPath);

                string relativePath = path.Replace(quickModFolderPath, string.Empty);

                if (!updatedPath.Contains(relativePath))
                    updatedPath.Add(relativePath);

                //logger.LogInfo($"New file detected: {Path.GetFileName(e.FullPath)}");

                waitTimer = MaidLoader.quickModTimer.Value;
                isNeedRefresh = true;
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
        private IEnumerator RefreshCO()
        {
            while (true)
            {
                while (!isNeedRefresh)
                    yield return new WaitForSecondsRealtime(1);

                while (isNeedRefresh && waitTimer > 0)
                {
                    waitTimer--;
                    CornerMessage.DisplayMessage($"New items added, AutoRefresh in {waitTimer}s", 1f);
                    yield return new WaitForSecondsRealtime(1);
                }

                MaidLoader.instance.StartCoroutine(Refresh());
                isNeedRefresh = false;
            }
        }     


        /// <summary>
        /// Refresh the File system and edit menus as a Coroutine.
        /// </summary>
        /// <returns></returns>
        public IEnumerator Refresh()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            // Create a thread and wait for UpdateFileSystem to be done.
            Task update = Task.Factory.StartNew(() =>
            {
                UpdateFileSystem();
            });
            yield return new WaitUntil(() => update.IsCompleted == true);
            
            if (update.IsFaulted)
            {
                logger.LogError($"QuickMod encountered an error, Refresh was canceled. One of your last added mod could be faulty or couldn't be read.");
                yield break;
            }

            //Parse added .menu
            InitMenu();

            sw.Stop();

            CornerMessage.DisplayMessage("Refresh over, new files added.", 6);
            logger.LogInfo($"QM File System updated in {sw.ElapsedMilliseconds}ms");
        }


        // Build the basic needed for a new FileSystem
        // Don't forget than COM's FileSystem works on a folder basis relative to the game's path.
        // This is the bit that is more likely to break.
        private void UpdateFileSystem()
        {   
            logger.LogInfo("Updating QM File System");
            QuickModFileSystem newFS = new();

            newFS.SetBaseDirectory(quickModFolderPath);

            foreach (string str in updatedPath)
                newFS.AddFolder(str);

            string[] folders = newFS.GetList(string.Empty, AFileSystemBase.ListType.AllFolder);
            foreach (string folder in folders)
            {
                if (newFS.AddAutoPath(folder))
                {
                    //logger.LogInfo($"Folder added: {folder}");
                }
            }

            // keep the old FS to delete later
            QuickModFileSystem oldFS = qmFileSystem;
            qmFileSystem = newFS;

            // delete the old FS
            oldFS.Dispose();
        }   

        /// <summary>
        /// Do everything needed to add a .menu to the edit mode panels.
        /// </summary>
        private void InitMenu()
        {
            // Get all .menu from QuickMod FileSystem
            List<string> files = qmFileSystem.GetFileListAtExtension("menu").ToList();

            if (files.Count == 0 || files == null)
                return;

            //Remove already added .menu
            foreach (string file in addedMenus)
                files.Remove(file);

            if (files.Count == 0)
                return;

            foreach (string menu in files)
                logger.LogInfo($"New menu found: {menu}");


            // Try to find SceneEdit
            SceneEdit sceneEdit = GameObject.Find("__SceneEdit__").GetComponent<SceneEdit>();

            List<SceneEdit.SMenuItem> menuList = new List<SceneEdit.SMenuItem>(files.Count);
            Dictionary<int, List<int>> menuGroupMemberDic = new Dictionary<int, List<int>>();

            // Go through all added .menu and add them to the already existing SceneEdit lists
            foreach (string strFileName in files)
            {
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

                        // check for _Zn parents.
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
                MaidLoader.quickMod.Start();
            }
        }
    }
}
