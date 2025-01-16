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
        private readonly ManualLogSource logger = MaidLoader.logger;
        public static event EventHandler<RefreshEventArgs> Refreshed;

        private readonly string gamePath = $"{UTY.gameProjectPath}\\";
        private List<string> newMenus = new();

        public RefreshMod()
        {
            EditModeUI.Init();
        }

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

            //Get added .menu files
            string[] addedMenus = { };
            Task filterAdded = Task.Factory.StartNew(() =>
            {
                addedMenus = FilterMenus(newFSMenus, oldFSMenus).ToArray();
            });
            

            //Get deleted .menu files
            string[] deletedMenus = { };
            Task filterDeleted = Task.Factory.StartNew(() =>
            {
                deletedMenus = FilterMenus(oldFSMenus, newFSMenus).ToArray();
            });

            yield return new WaitUntil(() => filterAdded.IsCompleted && filterDeleted.IsCompleted);

            //Raise Refresh Event
            Refreshed?.Invoke(this, new RefreshEventArgs(addedMenus, deletedMenus));

            // Remove deleted .menu from the game's UI, only if edit mode is enabled
            if (deletedMenus != null && deletedMenus.Length != 0 && SceneManager.GetActiveScene().buildIndex == 5)
            {
                EditModeUI.DeleteMenuIcon(deletedMenus.Select(m => Path.GetFileName(m)).ToArray());
            }

            //Add eventual .menu to the game's UI, only is edit mode is enabled.
            newMenus.AddRange(addedMenus);
            if (newMenus != null && newMenus.Count != 0)
            {
                if (SceneManager.GetActiveScene().buildIndex == 5)
                {
                    EditModeUI.AddMenuIcon(newMenus);
                    newMenus.Clear();
                }
                else
                {
                    logger.LogInfo("Edit mode not started. Integration of new .menu to the UI postponed.");
                }
            }

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

        //Update the game's mod FileSystem
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

        // Faster way to filter menus
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

        internal static class InitPatch
        {
            // Adding .menu to the UI
            [HarmonyPatch(typeof(SceneEdit), nameof(SceneEdit.OnCompleteFadeIn))]
            [HarmonyPostfix]
            internal static void OnCompleteFadeIn_Postfix()
            {
                if (MaidLoader.refreshMod.newMenus.Count > 0)
                {
                    MaidLoader.logger.LogInfo("Adding Mod folder's postponed .menu to the UI");
                    EditModeUI.AddMenuIcon(MaidLoader.refreshMod.newMenus);
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
