using BepInEx.Logging;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System;
using Mono.Cecil;

namespace COM3D2.MaidLoader
{
    public class RefreshMod
    {
        private readonly ManualLogSource logger = MaidLoader.logger;
        public static event EventHandler<RefreshEventArgs> Refreshed;

        private readonly string gamePath = $"{UTY.gameProjectPath}\\";
        private List<string> newMenus = new();


        /// <summary>
        /// Refresh the File system and edit menus as a Coroutine.
        /// </summary>
        public IEnumerator RefreshCo()
        {
            Stopwatch swRefresh = new Stopwatch();
            swRefresh.Start();

            //recover existing .menu from the old FileSystem before it gets disposed
            HashSet<string> oldFSMenus = new(GameUty.FileSystemMod.GetFileListAtExtension(".menu"));

            // Create a thread and wait for UpdateFileSystem to be done.
            Task update = Task.Factory.StartNew(() =>
            {
                UpdateFileSystem();
            });
            yield return new WaitUntil(() => update.IsCompleted);

            if (update.IsFaulted)
            {
                logger.LogError(update.Exception.InnerException);

                logger.LogError($"Mod Refresh encountered an error, refresh was canceled. One of your last added mod could be faulty or couldn't be read.");
                yield break;
            }

            Stopwatch swMenus = new();
            swMenus.Start();

            //recover new .menu from the new FileSystem
            HashSet<string> newFSMenus = new(GameUty.FileSystemMod.GetFileListAtExtension(".menu"));
            logger.LogMessage($"Recover newMenus: {swMenus.ElapsedMilliseconds}");

            //Get added .menu files
            string[] addedMenus = { };
            Task filterAdded = Task.Factory.StartNew(() =>
            {
                addedMenus = FilterMenus(newFSMenus, oldFSMenus).ToArray();
                logger.LogMessage($"Filter Added: {swMenus.ElapsedMilliseconds}");
            });
            

            //Get deleted .menu files
            string[] deletedMenus = { };
            Task filterDeleted = Task.Factory.StartNew(() =>
            {
                deletedMenus = FilterMenus(oldFSMenus, newFSMenus).ToArray();
                logger.LogMessage($"Filter Deleted: {swMenus.ElapsedMilliseconds}");
            });

            yield return new WaitUntil(() => filterAdded.IsCompleted && filterDeleted.IsCompleted);

            //Raise Refresh Event
            Refreshed?.Invoke(this, new RefreshEventArgs(addedMenus, deletedMenus));
            logger.LogMessage($"Raise Event: {swMenus.ElapsedMilliseconds}");

            // Remove deleted .menu from the game's UI, only if edit mode is enabled
            if (deletedMenus != null && deletedMenus.Length != 0 && SceneManager.GetActiveScene().buildIndex == 5)
            {
                DeleteMenuIcon(deletedMenus.Select(m => Path.GetFileName(m)).ToArray());
            }
            logger.LogMessage($"Remove deleted Menus: {swMenus.ElapsedMilliseconds}");

            //Add eventual .menu to the game's UI, only is edit mode is enabled.
            newMenus.AddRange(addedMenus);
            if (newMenus != null && newMenus.Count != 0)
            {
                if (SceneManager.GetActiveScene().buildIndex == 5)
                {
                    AddMenuIcon(newMenus);
                    newMenus.Clear();
                }
                else
                {
                    logger.LogInfo("Edit mode not started. Integration of new .menu to the UI postponed.");
                }
            }
            logger.LogMessage($"Add new Menus: {swMenus.ElapsedMilliseconds}");

            swMenus.Stop();
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", swMenus.Elapsed.Hours, swMenus.Elapsed.Minutes, swMenus.Elapsed.Seconds, swMenus.Elapsed.Milliseconds);
            logger.LogInfo($"Menus filtering done in {elapsedTime}");

            //Required to add .asset references back.
            AssetManager.InitPostfix();

            swRefresh.Stop();
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", swRefresh.Elapsed.Hours, swRefresh.Elapsed.Minutes, swRefresh.Elapsed.Seconds, swRefresh.Elapsed.Milliseconds);
            logger.LogInfo($"Mod updated in {elapsedTime}");

            CornerMessage.DisplayMessage("Refresh over, new files added.", 6);
        }

        private void UpdateFileSystem()
        {
            logger.LogInfo("Updating Mod File System");
            Stopwatch FSsw = new Stopwatch();
            FSsw.Start();

            FileSystemWindows newFS = new();

            newFS.SetBaseDirectory(gamePath);
            newFS.AddFolder("Mod");
            newFS.AddAutoPathForAllFolder(true);

            
            while (!newFS.IsFinishedAddAutoPathJob(true))
            {
                // Yes I know, but it's how Kiss made it.
            }
            newFS.ReleaseAddAutoPathJob();

            // keep the old FS to delete later
            FileSystemWindows oldFS = GameUty.m_ModFileSystem;
            GameUty.m_ModFileSystem = newFS;

            FSsw.Stop();
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", FSsw.Elapsed.Hours, FSsw.Elapsed.Minutes, FSsw.Elapsed.Seconds, FSsw.Elapsed.Milliseconds / 10);
            logger.LogInfo($"Mod File System updated in {elapsedTime}");

            //Update Cache
            ModPriority.BuildModCache();

            // delete the old FS
            oldFS.Dispose();
        }

        private void AddMenuIcon(List<string> menus)
        {
            logger.LogInfo($"Adding {menus.Count} menus. This might freeze the game for a short time.");

            // Try to find SceneEdit
            SceneEdit sceneEdit = GameObject.Find("__SceneEdit__").GetComponent<SceneEdit>();

            List<SceneEdit.SMenuItem> menuItemList = new(menus.Count);
            Dictionary<int, List<int>> menuGroupMemberDic = new();

            // Go through all added .menu and add them to the already existing SceneEdit lists
            foreach (string menu in menus)
            {
                logger.LogInfo($"\tAdding: {Path.GetFileName(menu)}");
                SceneEdit.SMenuItem mi = new();

                // Parse the actual .menu
                if (SceneEdit.GetMenuItemSetUP(mi, menu, false))
                {
                    // ignore is this .menu is made for a man or has no icon
                    if (!mi.m_bMan && (mi.m_texIconRef != null))
                    {
                        //Doesn't look like much, but this is the most important part.
                        sceneEdit.AddMenuItemToList(mi);
                        menuItemList.Add(mi);

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
                            mi.m_listMember = new List<SceneEdit.SMenuItem>
                            {
                                mi
                            };
                        }
                    }
                }
            }

            // Deals with .mod and sub menus
            sceneEdit.StartCoroutine(sceneEdit.FixedInitMenu(menuItemList, sceneEdit.m_menuRidDic, menuGroupMemberDic));
            sceneEdit.StartCoroutine(sceneEdit.CoLoadWait());
        }

        private void DeleteMenuIcon(string[] deletedMenus)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            SceneEdit sceneEdit = GameObject.Find("__SceneEdit__").GetComponent<SceneEdit>();

            foreach (SceneEdit.SCategory category in sceneEdit.m_listCategory)
            {
                foreach (SceneEdit.SPartsType part in category.m_listPartsType)
                {
                    for (int i = part.m_listMenu.Count - 1; i >= 0; i--)
                    {
                        if (deletedMenus.Contains(part.m_listMenu[i].m_strMenuFileName))
                        {
                            SceneEdit.SMenuItem mi = part.m_listMenu[i];

                            logger.LogInfo($"\tRemoving: {mi.m_strMenuFileName}");

                            //Remove the SMenuItem from lists referencing it.
                            part.m_listMenu.Remove(mi);
                            sceneEdit.m_menuRidDic.Remove(mi.m_nMenuFileRID);

                            //Destroy relevant parts from the SMenuItem to make it disapear from the UI.
                            UnityEngine.Object.Destroy(mi.m_goButton);
                            //UnityEngine.Object.Destroy(mi.m_texIcon);
                        }
                    }
                }
            }

            stopwatch.Stop();
            logger.LogInfo($"Old Menus removed from the UI in {stopwatch.ElapsedMilliseconds}ms.");
        }

        private IEnumerable<string> FilterMenus(HashSet<string> set1, HashSet<string> set2)
        {
            foreach (string element in set1)
            {
                if (!set2.Contains(element))
                {
                    yield return element;
                }
            }
        }

        internal class InitPatch
        {
            // Adding .menu to the UI
            [HarmonyPatch(typeof(SceneEdit), nameof(SceneEdit.OnCompleteFadeIn))]
            [HarmonyPostfix]
            internal static void OnCompleteFadeIn_Postfix()
            {
                if (MaidLoader.refreshMod.newMenus.Count > 0)
                {
                    MaidLoader.logger.LogInfo("Adding Mod folder's postponed .menu to the UI");
                    MaidLoader.refreshMod.AddMenuIcon(MaidLoader.refreshMod.newMenus);
                    MaidLoader.refreshMod.newMenus.Clear();
                }
            }
        }

        public class RefreshEventArgs : EventArgs
        {
            public RefreshEventArgs(string[] newMenus, string[] deletedMenus)
            {
                NewMenus = newMenus;
                DeletedMenus = deletedMenus;
            }

            public string[] NewMenus { get; private set; }
            public string[] DeletedMenus { get; private set; }
        }
    }
}
