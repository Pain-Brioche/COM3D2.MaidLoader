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

namespace COM3D2.MaidLoader
{
    public class QuickMod
    {
        private ManualLogSource logger = MaidLoader.logger;
        private FileSystemWatcher watcher;

        private List<string> addedMenus = new();
        private List<string> updatedPath = new();
        private static string gamePath = UTY.gameProjectPath.Replace('/', '\\');
        private static string modPath = Path.Combine(gamePath, "Mod");
        private static string quickModFolder = MaidLoader.quickModFolder.Value;
        private static string quickModPath = Path.Combine(gamePath, quickModFolder);
        private static int quickModTimer = MaidLoader.quickModTimer.Value * 1000;
        private static readonly string[] validFiles = { ".menu", ".tex", ".model", ".mate", "*.psk" };

        private bool useModFolder = MaidLoader.useModFolder.Value;

        private Stopwatch waitTimer = new Stopwatch();
        private static bool isUpdated = true;

        private static Harmony harmony;
        private static Harmony harmony2;

        public static FileSystemWindows qmFileSystem = new();


        internal QuickMod()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(InitPatch));
        }

        private void Start()
        {
            harmony2 = Harmony.CreateAndPatchAll(typeof(FileSystemModPatch));

            if (!useModFolder && !Directory.Exists(quickModPath))
                Directory.CreateDirectory(quickModPath);


            if (useModFolder)
            {
                //Get all registered files that already existed at game launch to avoid doubles
                List<string> menus = GameUty.FileSystemMod.GetFileListAtExtension("menu").ToList();
                foreach (string menu in menus)
                    addedMenus.Add(Path.GetFileName(menu));
            }
            else
            {
                //Gather items already in QuickMod's folder if global load isn't used.
                UpdateFileSystem();
                InitMenu();
            }

            //Starts a Coroutine to periodically check if files were added.
            //MaidLoader.instance.StartCoroutine(AutomaticRefreshCO());

            Monitor();
        }

        /// <summary>
        /// Monitor changes in the Mod folder
        /// </summary>
        private void Monitor()
        {
            string monitoredFolder = useModFolder ? modPath : quickModPath;

            watcher = new FileSystemWatcher(monitoredFolder);
            logger.LogInfo($"Monitoring started in {monitoredFolder}");

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

                string relativePath = path.Replace(gamePath, string.Empty);

                if (!updatedPath.Contains(relativePath))
                    updatedPath.Add(relativePath);

                logger.LogInfo($"New file detected: {Path.GetFileName(e.FullPath)}");
            }
        }


        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            logger.LogWarning($"COM's FileSystem does NOT support deleting mods!\nAny item using {Path.GetFileName(e.FullPath)} will give an error unless a replacement is found.");
        }

        /*
        /// <summary>
        /// Periodically checks if the files system needs to be rebuilt.
        /// Rebuild it if needed and x seconds ellapsed since last trigger.
        /// </summary>
        private IEnumerator AutomaticRefreshCO()
        {
            for (; ; )
            {
                if (rebuildFileSystem)
                {
                    if (waitTimer.ElapsedMilliseconds >= quickModTimer)
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();

                        logger.LogInfo("Updating QM File System");
                        // Rebuild the qmFileSystem
                        if (useModFolder)
                        {
                            UpdateFileSystem();
                        }
                        else
                        {
                            UpdateFileSystem();
                        }

                        //Parse added .menu
                        InitMenu();
                        sw.Stop();

                        CornerMessage.DisplayMessage("New files added.", 6);
                        logger.LogInfo($"QM File System updated in {sw.ElapsedMilliseconds}ms");

                        waitTimer.Reset();
                        rebuildFileSystem = false;
                    }
                    else
                    {
                        string message = $"Adding new files in {Math.Round((double)((quickModTimer - waitTimer.ElapsedMilliseconds) / 1000))}s";
                        CornerMessage.DisplayMessage(message, 1);
                        logger.LogInfo(message);
                    }
                }
                yield return new WaitForSeconds(1);
            }
        }
        */

        //Just checks the value of isUpdated, used for the yield bellow
        private bool IsUpdated() => isUpdated;

        public IEnumerator Refresh()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            // wait for UpdateFileSystem to be done
            isUpdated = false;
            UpdateFileSystem();
            yield return new WaitUntil(IsUpdated);
             
            //Parse added .menu
            InitMenu();
            sw.Stop();

            CornerMessage.DisplayMessage("Refresh done, new files added.", 6);
            logger.LogInfo($"QM File System updated in {sw.ElapsedMilliseconds}ms");
        }


        // Build the basic needed for a new FileSystem
        // Don't forget than COM's FileSystem works on a folder basis relative to the game's path.
        // This is the bit that is more likely to break.
        private void UpdateFileSystem()
        {
            // Start this as a separate thread
            Task.Factory.StartNew(() =>
            {
                logger.LogInfo("Updating QM File System");
                FileSystemWindows newFS = new();

                newFS.SetBaseDirectory(gamePath);

                foreach (string str in updatedPath)
                    newFS.AddFolder(str);

                string[] folders = newFS.GetList(string.Empty, AFileSystemBase.ListType.AllFolder);
                foreach (string folder in folders)
                {
                    if (newFS.AddAutoPath(folder))
                        logger.LogInfo($"Folder added: {Path.GetFileName(folder)}");
                }

                // keep the old FS to delete later
                FileSystemWindows oldFS = qmFileSystem;
                qmFileSystem = newFS;

                // delete the old FS
                oldFS.Dispose();
            });
            isUpdated = true;
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


        internal class FileSystemModPatch
        {
            // Patch GameUty.FileOpen to also look inside QuickMod FileSystem.
            [HarmonyPatch(typeof(GameUty), nameof(GameUty.FileOpen))]
            [HarmonyPostfix]
            internal static void FileOpen_Postfix(string fileName, ref AFileBase __result)
            {
                if (__result == null)
                {
                    bool exists = qmFileSystem.IsExistentFile(fileName);

                    if (!exists)
                        MaidLoader.logger.LogMessage($"{fileName} doesn't exist");
                    else
                        __result = qmFileSystem.FileOpen(fileName);
                }
            }
        }
    }
}
