using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;

namespace COM3D2.MaidLoader
{
    internal class RefreshMod
    {
        private ManualLogSource logger = MaidLoader.logger;

        string gamePath = UTY.gameProjectPath + "\\";
        //string modPath = Path.Combine(UTY.gameProjectPath, "Mod");
        List<string> menuList = GameUty.FileSystemMod.GetFileListAtExtension(".menu").ToList();


        /// <summary>
        /// Refresh the File system and edit menus as a Coroutine.
        /// </summary>
        public IEnumerator RefreshCo()
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
                logger.LogError(update.Exception.InnerException);

                logger.LogError($"Mod Refresh encountered an error, refresh was canceled. One of your last added mod could be faulty or couldn't be read.");
                yield break;
            }

            //Get new .menu files
            logger.LogInfo("Looking for added files.");
            string[] newMenus = null;
            Task getNewMenus = Task.Factory.StartNew(() =>
            {
                newMenus = GameUty.FileSystemMod.GetFileListAtExtension(".menu").Except(menuList).ToArray();
                //newMenus = Directory.GetFiles(modPath, "*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f).ToLower() == ".menu").Select(x => x.Replace(modPath + "\\", string.Empty).ToLower()).Except(menus).ToArray();
            });
            yield return new WaitUntil(() => getNewMenus.IsCompleted == true);

            //Parse added .menu
            if (newMenus != null && newMenus.Length != 0)
            {
                InitMenu(newMenus);
                menuList.AddRange(menuList);
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

        /// <summary>
        /// Do everything needed to add a .menu to the edit mode panels.
        /// </summary>
        private void InitMenu(string[] newMenus)
        {
            logger.LogInfo($"Adding {newMenus.Length} menus. This might freeze the game for a short time.");

            // Try to find SceneEdit
            SceneEdit sceneEdit = GameObject.Find("__SceneEdit__").GetComponent<SceneEdit>();

            List<SceneEdit.SMenuItem> menuList = new List<SceneEdit.SMenuItem>(newMenus.Length);
            Dictionary<int, List<int>> menuGroupMemberDic = new Dictionary<int, List<int>>();

            // Go through all added .menu and add them to the already existing SceneEdit lists
            foreach (string menu in newMenus)
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
            }

            // Deals with .mod and sub menus
            sceneEdit.StartCoroutine(sceneEdit.FixedInitMenu(menuList, sceneEdit.m_menuRidDic, menuGroupMemberDic));
            sceneEdit.StartCoroutine(sceneEdit.CoLoadWait());
        }
    }
}
