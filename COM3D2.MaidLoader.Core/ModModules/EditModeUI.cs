using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace COM3D2.MaidLoader
{
    internal static class EditModeUI
    {
        private static readonly ManualLogSource logger = MaidLoader.logger;
        private static List<string> list = new();

        internal static void Init()
        {
            Harmony.CreateAndPatchAll(typeof(EditModeUI));
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// Do everything needed to add a .menu to the edit mode panels.
        /// </summary>
        internal static void AddMenuIcon(List<string> menus)
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

        internal static void DeleteMenuIcon(string[] deletedMenus)
        {
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
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "SceneEdit")
                list.Clear();
        }

        //Prevent duplicated sliders
        [HarmonyPatch(typeof(SceneEdit), nameof(SceneEdit.AddMenuItemToList))]
        [HarmonyPrefix]
        internal static bool AddMenuItemToList_Prefix(SceneEdit.SMenuItem f_mi, ref bool __result)
        {
            __result = true;
            if (f_mi.m_nSliderValue == 500)
            {
                if (list.Contains(f_mi.m_mpn.ToString()))
                {
                    //logger.LogWarning($"{f_mi.m_mpn} Rejected");
                    return false;
                }
                list.Add(f_mi.m_mpn.ToString());
            }
            return true;
        }
    }
}
