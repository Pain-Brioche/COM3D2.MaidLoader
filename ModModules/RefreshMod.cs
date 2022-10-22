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

namespace COM3D2.MaidLoader
{
    public class RefreshMod
    {
        private ManualLogSource logger = MaidLoader.logger;
        public static event EventHandler<RefreshEventArgs> Refreshed;

        string gamePath = UTY.gameProjectPath + "\\";
        //private List<string> menuList = GameUty.FileSystemMod.GetFileListAtExtension(".menu").ToList();
        private List<string> addedMenus = new();


        /// <summary>
        /// Refresh the File system and edit menus as a Coroutine.
        /// </summary>
        public IEnumerator RefreshCo()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            //recover existing .menu from the old FileSystem
            string[] oldFSMenus = GameUty.FileSystemMod.GetFileListAtExtension(".menu");

            // Create a thread and wait for UpdateFileSystem to be done.
            Task update = Task.Factory.StartNew(() =>
            {
                UpdateFileSystem();
            });
            yield return new WaitUntil(() => update.IsCompleted == true);

            if (update.IsFaulted)
            {
                logger.LogError(update.Exception.InnerException);

                logger.LogError($"Mod Refresh encountered an error, refresh was canceled. One of your last added mod could be faulty or couldn't be read.");
                yield break;
            }

            //recover new .menu from the new FileSystem
            string[] newFSMenus = GameUty.FileSystemMod.GetFileListAtExtension(".menu");

            //Get added .menu files, keep them aside in case edit mode isn't loaded.
            addedMenus.AddRange(newFSMenus.Except(oldFSMenus));

            //Get deleted .menu files
            string[] deletedMenus = oldFSMenus.Except(newFSMenus).ToArray();

            //Raise Refresh Event
            Refreshed?.Invoke(this, new RefreshEventArgs(addedMenus.ToArray(), deletedMenus));

            // Remove deleted .menu from the game's UI, only if edit mode is enabled
            if (deletedMenus != null && deletedMenus.Length != 0 && SceneManager.GetActiveScene().buildIndex == 5)
            {
                DeleteMenuIcon(deletedMenus.Select(m => Path.GetFileName(m)).ToArray());
            }

            //Add eventual .menu to the game's UI, only is edit mode is enabled.
            if (addedMenus != null && addedMenus.Count != 0)
            {
                if(SceneManager.GetActiveScene().buildIndex == 5)
                {
                    AddMenuIcon(addedMenus);
                    addedMenus.Clear();
                }
                else
                {
                    logger.LogInfo("Edit mode not started. Integration of new .menu to the UI postponed.");
                } 
            }
            
            sw.Stop();

            CornerMessage.DisplayMessage("Refresh over, new files added.", 6);
            logger.LogInfo($"Mod updated in {sw.ElapsedMilliseconds}ms");
        }

        private void UpdateFileSystem()
        {
            logger.LogInfo("Updating Mod File System");
            FileSystemWindows newFS = new();

            newFS.SetBaseDirectory(gamePath);
            newFS.AddFolder("Mod");
            newFS.AddAutoPathForAllFolder(true);

            // Yes I know, but it's how Kiss made it.
            while (!newFS.IsFinishedAddAutoPathJob(true))
            {
            }
            newFS.ReleaseAddAutoPathJob();

            // keep the old FS to delete later
            FileSystemWindows oldFS = GameUty.m_ModFileSystem;
            GameUty.m_ModFileSystem = newFS;

            // delete the old FS
            oldFS.Dispose();
        }

        private void AddMenuIcon(List<string> menus)
        {
            logger.LogInfo($"Adding {menus.Count} menus. This might freeze the game for a short time.");

            // Try to find SceneEdit
            SceneEdit sceneEdit = GameObject.Find("__SceneEdit__").GetComponent<SceneEdit>();

            List<SceneEdit.SMenuItem> menuItemList = new List<SceneEdit.SMenuItem>(menus.Count);
            Dictionary<int, List<int>> menuGroupMemberDic = new Dictionary<int, List<int>>();

            // Go through all added .menu and add them to the already existing SceneEdit lists
            foreach (string menu in menus)
            {
                logger.LogInfo($"\tAdding: {Path.GetFileName(menu)}");
                SceneEdit.SMenuItem mi = new SceneEdit.SMenuItem();

                // Parse the actual .menu
                if (SceneEdit.GetMenuItemSetUP(mi, menu, false))
                {
                    // ignore is this .menu is made for a man or has no icon
                    if (!mi.m_bMan && !(mi.m_texIconRef == null))
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
                            mi.m_listMember = new List<SceneEdit.SMenuItem>();
                            mi.m_listMember.Add(mi);
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
                foreach(SceneEdit.SPartsType part in category.m_listPartsType)
                {
                    for(int i = part.m_listMenu.Count - 1; i >=0; i--)
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



        internal class InitPatch
        {
            // Adding .menu to the UI
            [HarmonyPatch(typeof(SceneEdit), nameof(SceneEdit.OnCompleteFadeIn))]
            [HarmonyPostfix]
            internal static void OnCompleteFadeIn_Postfix()
            {
                if (MaidLoader.refreshMod.addedMenus.Count > 0)
                {
                    MaidLoader.logger.LogInfo("Adding Mod folder's postponed .menu to the UI");
                    MaidLoader.refreshMod.AddMenuIcon(MaidLoader.refreshMod.addedMenus);
                    MaidLoader.refreshMod.addedMenus.Clear();
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
