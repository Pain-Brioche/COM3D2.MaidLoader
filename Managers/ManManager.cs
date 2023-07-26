using HarmonyLib;
using System;
using static SceneEdit;

namespace COM3D2.MaidLoader
{
    internal static class ManManager
    {
        private static Harmony harmony;
        internal static void Init()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(ManManager));
        }

        [HarmonyPatch(typeof(PhotoManEditManager), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void ManManagerConstructor_Postfix(PhotoManEditManager __instance)
        {
            MPN body = (MPN)Enum.Parse(typeof(MPN), "body");

            foreach (string menuFile in GameUty.ModOnlysMenuFiles)
            {
                //Only retains readable .menu containing mhead or mbody
                if (menuFile.Contains("mhead") || menuFile.Contains("mbody"))
                {
                    PhotoManEditManager.menu_file_name_list_.Add(menuFile);

                    SMenuItem sMenuItem = new SMenuItem();
                    if (GetMenuItemSetUP(sMenuItem, menuFile, true))
                    {
                        //Add them to their corresponding list.
                        if (sMenuItem.m_mpn == body)
                        {
                            MaidLoader.logger.LogDebug($"{menuFile} added to body");
                            __instance.man_body_menu_list.Add(sMenuItem);
                        }
                        else
                        {
                            MaidLoader.logger.LogDebug($"{menuFile} added to head");
                            __instance.man_head_menu_list.Add(sMenuItem);
                        }
                    }
                }
            }
        }
    }
}
