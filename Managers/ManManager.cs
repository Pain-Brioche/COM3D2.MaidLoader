using HarmonyLib;
using static SceneEdit;

namespace COM3D2.MaidLoader
{
    internal class ManManager
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
            foreach (string menuFile in GameUty.ModOnlysMenuFiles)
            {
                //Only retains readable .menu containing mhead or mbody
                if (menuFile.Contains("mhead") || menuFile.Contains("mbody"))
                {
                    SMenuItem sMenuItem = new SMenuItem();
                    if (GetMenuItemSetUP(sMenuItem, menuFile, true))
                    {
                        //Add them to their corresponding list.
                        if (sMenuItem.m_mpn == MPN.body)
                        {
                            __instance.man_body_menu_list.Add(sMenuItem);
                        }
                        else if (sMenuItem.m_mpn == MPN.head)
                        {
                            __instance.man_head_menu_list.Add(sMenuItem);
                        }
                    }
                }
            }
        }
    }
}
