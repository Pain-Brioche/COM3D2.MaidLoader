using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using BepInEx.Logging;

namespace COM3D2.MaidLoader
{
    internal static class NeiManager
    {
        internal static Harmony harmony;
        internal static ManualLogSource logger = MaidLoader.logger;
        internal static bool isAppendBGObjectDone = false;


        internal static void Init()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(NeiManagerPatches));
        }

        /*
        Methods bellow are used to add custom assets to different parts of the game.
        Original code from Neerhom's Modloader
        Folder structure was kept for legacy support
        */

        // Append Backgrounds by reading modded .nei
        // Added an auto append.
        internal static void AppendBG()
        {
            //Retreive files located in the PhotoBG_NEI Mod folder


            // Fix attempt, since 2.38 GetFile doesn't filter by folder            
            //string[] bg_nelist = GameUty.FileSystemMod.GetList("Mod", AFileSystemBase.ListType.AllFile);
            IEnumerable<string> bg_nelist = ModPriority.modCache.Where(p => p.Contains("PhotoBG_NEI")).Select(f => Path.GetFileName(f));            
            
            List<PhotoBGData> customBGData = new();

            //Go through each .nei files
            foreach (string str in bg_nelist)
            {
                string filename = Path.GetFileName(str);

                if (Path.GetExtension(filename) == ".nei" && filename != "phot_bg_list.nei")
                {
                    using (AFileBase aFileBase2 = GameUty.FileSystemMod.FileOpen(filename))
                    {
                        using (CsvParser csvParser = new CsvParser())
                        {
                            if (csvParser.Open(aFileBase2))
                            {
                                // .nei are read by row (r) then column
                                for (int r = 1; r < csvParser.max_cell_y; r++)
                                {
                                    int c = 1;
                                    PhotoBGData photoBGData = new PhotoBGData();
                                    photoBGData.id = "";
                                    photoBGData.category = csvParser.GetCellAsString(c++, r);
                                    photoBGData.name = csvParser.GetCellAsString(c++, r);
                                    photoBGData.create_prefab_name = csvParser.GetCellAsString(c++, r);
                                    //assign id from prefab/bundles string
                                    //this is done because save/load of objects in photomode is based on id
                                    if (!string.IsNullOrEmpty(photoBGData.create_prefab_name))
                                    {
                                        photoBGData.id = photoBGData.create_prefab_name.GetHashCode().ToString();
                                        // this feels wrong, but i suspect the KISS might convert id to int at some point
                                        // so i'd rather be on safe side
                                    }

                                    //Check is the corresponding .asset_bg do exists before adding it.
                                    string check = csvParser.GetCellAsString(c++, r);
                                    if (string.IsNullOrEmpty(check) || GameUty.BgFiles.ContainsKey(photoBGData.create_prefab_name.ToLower() + ".asset_bg"))
                                    {
                                        customBGData.Add(photoBGData);
                                        PhotoBGData.data.Add(photoBGData);
                                        logger.LogDebug($"Adding: {photoBGData.name} to {photoBGData.category}");
                                    }
                                }
                            }
                            else
                            {
                                logger.LogWarning($"Skipping invalid file: Mod/{str}");
                            }
                        }
                    }
                }
                // Auto append loose .asset_bg put in the PhotoBG_NEI folder
                // .nei option is more precise, although this may be easier for end users
                else if (Path.GetExtension(filename) == ".asset_bg")
                {
                    PhotoBGData photoBGData = new PhotoBGData();
                    photoBGData.id = "";
                    photoBGData.category = "AutoBG"; //Always the same category (Day)
                    photoBGData.name = Path.GetFileNameWithoutExtension(filename);
                    photoBGData.create_prefab_name = Path.GetFileNameWithoutExtension(filename);

                    //Same thing as before
                    if (!string.IsNullOrEmpty(photoBGData.create_prefab_name))
                    {
                        photoBGData.id = photoBGData.create_prefab_name.GetHashCode().ToString();
                    }

                    customBGData.Add(photoBGData);
                    PhotoBGData.data.Add(photoBGData);
                    logger.LogDebug($"Auto Adding: {photoBGData.name} to category {photoBGData.category}");
                }
            }

            // Add custom categories
            HashSet<string> customCategories = new HashSet<string>();
            // Add already existing categories so they aren't added twice.
            foreach(string term in PhotoBGData.popup_category_term_list)
            {
                string category = term.Remove(0,24);
                customCategories.Add(category);
            }

            foreach (var customBG in customBGData)
            {
                if (!PhotoBGData.category_list.ContainsKey(customBG.category))
                {
                    PhotoBGData.category_list.Add(customBG.category, new List<PhotoBGData>());
                }
                PhotoBGData.category_list[customBG.category].Add(customBG);


                if (!customCategories.Contains(customBG.category))
                {
                    logger.LogDebug($"New category added: {customBG.category}");
                    PhotoBGData.popup_category_list.Add(new KeyValuePair<string, UnityEngine.Object>(customBG.category, null));
                    PhotoBGData.popup_category_term_list.Add("ScenePhotoMode/BG/カテゴリー/" + customBG.category);
                    customCategories.Add(customBG.category);
                }
            }
        }

        // Append Backgrounds Object by reading modded .nei
        // Added an auto append.
        internal static void AppendBGObject()
        {
            // Skip if this was already done.
            if (PhotoBGObjectData.category_list.ContainsKey("MaidLoader"))
                return;

            if (isAppendBGObjectDone)
                return;

            //Works pretty much the same way as AppendBG() with a different .nei parsing 
            List<PhotoBGObjectData> customBGObjectData = new List<PhotoBGObjectData>();

            // Fix attempt, since 2.38 GetFile doesn't filter by folder
            //string[] BgObj_list = GameUty.FileSystemMod.GetList("PhotoBG_OBJ_NEI", AFileSystemBase.ListType.AllFile);
            IEnumerable<string> BgObj_list = ModPriority.modCache.Where(p => p.Contains("PhotoBG_OBJ_NEI")).Select(f => Path.GetFileName(f));

            foreach (string str in BgObj_list)
            {
                string filename = Path.GetFileName(str);

                if (Path.GetExtension(filename) == ".nei" && filename != "phot_bg_object_list.nei")
                {
                    using (AFileBase aFileBase = GameUty.FileSystemMod.FileOpen(filename))
                    {
                        using (CsvParser csvParser = new CsvParser())
                        {
                            if (csvParser.Open(aFileBase))
                            {
                                for (int i = 1; i < csvParser.max_cell_y; i++)
                                {
                                    int num = 1;
                                    PhotoBGObjectData photoBGObjectData = new PhotoBGObjectData();
                                    photoBGObjectData.id = 0;
                                    photoBGObjectData.category = csvParser.GetCellAsString(num++, i);
                                    photoBGObjectData.name = csvParser.GetCellAsString(num++, i);
                                    photoBGObjectData.create_prefab_name = csvParser.GetCellAsString(num++, i);
                                    photoBGObjectData.create_asset_bundle_name = csvParser.GetCellAsString(num++, i);

                                    //assign id from prefab/bundles string, this is done because save/load of objects in photomode is based on id
                                    if (!string.IsNullOrEmpty(photoBGObjectData.create_prefab_name))
                                    {
                                        photoBGObjectData.id = photoBGObjectData.create_prefab_name.GetHashCode();
                                    }
                                    else if (!string.IsNullOrEmpty(photoBGObjectData.create_asset_bundle_name))
                                    {
                                        photoBGObjectData.id = photoBGObjectData.create_asset_bundle_name.GetHashCode();
                                    }
                                    string check = csvParser.GetCellAsString(num++, i);
                                    if (string.IsNullOrEmpty(check) || GameUty.BgFiles.ContainsKey(photoBGObjectData.create_asset_bundle_name.ToLower() + ".asset_bg"))
                                    {
                                        customBGObjectData.Add(photoBGObjectData);
                                        PhotoBGObjectData.data.Add(photoBGObjectData);
                                    }
                                }
                            }
                            else
                            {
                                logger.LogWarning($"Skipping invalid file: Mod/{str}");
                            }
                        }
                    }
                }
                // Auto append loose .asset_bg put in the PhotoBG_OBJ_NEI folder
                // .nei option is more precise, although this may be easier for end users

                else if (Path.GetExtension(filename) == ".asset_bg")
                {
                    PhotoBGObjectData photoBGObjectData = new PhotoBGObjectData();
                    photoBGObjectData.id = 0;
                    photoBGObjectData.category = "MaidLoader";
                    photoBGObjectData.name = Path.GetFileNameWithoutExtension(filename);
                    photoBGObjectData.create_prefab_name = string.Empty;
                    photoBGObjectData.create_asset_bundle_name = Path.GetFileNameWithoutExtension(filename);

                    customBGObjectData.Add(photoBGObjectData);
                    PhotoBGObjectData.data.Add(photoBGObjectData);

                    logger.LogDebug($"Auto Adding: {photoBGObjectData.name} to category {photoBGObjectData.category}");
                }
            }

            // Add loaded assets to the category they belong to.
            HashSet<string> hashSet2 = new HashSet<string>();
            // Add already existing categories so they aren't added twice.
            foreach (string term in PhotoBGObjectData.popup_category_term_list)
            {
                string category = term.Remove(0, 30);
                hashSet2.Add(category);
            }

            for (int j = 0; j < customBGObjectData.Count; j++)
            {
                if (!PhotoBGObjectData.category_list.ContainsKey(customBGObjectData[j].category))
                {
                    PhotoBGObjectData.category_list.Add(customBGObjectData[j].category, new List<PhotoBGObjectData>());
                }
                PhotoBGObjectData.category_list[customBGObjectData[j].category].Add(customBGObjectData[j]);

                if (!hashSet2.Contains(customBGObjectData[j].category))
                {
                    PhotoBGObjectData.popup_category_list.Add(new KeyValuePair<string, UnityEngine.Object>(customBGObjectData[j].category, null));
                    PhotoBGObjectData.popup_category_term_list.Add("ScenePhotoMode/背景オブジェクト/カテゴリー/" + customBGObjectData[j].category);
                    hashSet2.Add(customBGObjectData[j].category);
                }
            }

            isAppendBGObjectDone = true;
        }

        // Append Object categories and details by reading modded .nei
        internal static void AppendDeskData()
        {
            //string[] DeskItemList = GameUty.FileSystemMod.GetList("DeskItem_NEI", AFileSystemBase.ListType.AllFile);
            // Fix attempt, since 2.38 GetFile doesn't filter by folder
            IEnumerable<string> DeskItemList = ModPriority.modCache.Where(p => p.Contains("DeskItem_NEI")).Select(f => Path.GetFileName(f));

            foreach (string str in DeskItemList)
            {
                string file = Path.GetFileName(str);
                string extension = Path.GetExtension(file);

                // Adding MaidLoader's custom category
                if (!DeskManager.item_category_data_dic.ContainsKey(777))
                    DeskManager.item_category_data_dic.Add(777, "MaidLoader");
                

                // Check if the file is category type or detail type to determine the processing
                // Only files that contain category in their name are assumed to be category files all others nei files are assumbed to be detail files
                if (file.Contains("category") && extension == ".nei" && file != "desk_item_category.nei")
                {
                    using (AFileBase afileBase = GameUty.FileSystem.FileOpen(file))
                    {
                        using (CsvParser csvParser = new CsvParser())
                        {
                            if (csvParser.Open(afileBase))
                            {
                                for (int k = 1; k < csvParser.max_cell_y; k++)
                                {
                                    if (!csvParser.IsCellToExistData(0, k))
                                    {
                                        break;
                                    }
                                    int id = csvParser.GetCellAsInteger(0, k);
                                    string categoryName = csvParser.GetCellAsString(1, k);

                                    if (DeskManager.item_category_data_dic.ContainsKey(id))
                                        logger.LogWarning($"Category ID [{id}] already exists");
                                    else
                                        DeskManager.item_category_data_dic.Add(id, categoryName);                                    
                                }
                            }
                        }
                    }
                }

                else if (file != "desk_item_detail.nei" && extension == ".nei")
                {
                    using (AFileBase afileBase = GameUty.FileSystem.FileOpen(file))
                    {
                        using (CsvParser csvParser = new CsvParser())
                        {
                            if (csvParser.Open(afileBase))
                            {
                                for (int j = 1; j < csvParser.max_cell_y; j++)
                                {
                                    if (csvParser.IsCellToExistData(0, j))
                                    {
                                        int cellAsInteger2 = csvParser.GetCellAsInteger(0, j);
                                        DeskManager.ItemData itemData = new DeskManager.ItemData(csvParser, j);

                                        // check if it's a prefab data and add it if it is
                                        // is impossible to check if prefab exists in resources files
                                        // so if referenced prefab doesn't exist in game files, the entry isn't going to work properly in game
                                        if (!string.IsNullOrEmpty(itemData.prefab_name))
                                        {
                                            itemData.id = itemData.prefab_name.GetHashCode();
                                            if (!DeskManager.item_detail_data_dic.ContainsKey(itemData.id))
                                                DeskManager.item_detail_data_dic.Add(itemData.id, itemData);
                                        }

                                        // check if entry refers to asset bundle, and if it is, check if it exists before addding the data
                                        else if (!string.IsNullOrEmpty(itemData.asset_name) && GameUty.BgFiles.ContainsKey(itemData.asset_name + ".asset_bg"))
                                        {
                                            itemData.id = itemData.asset_name.GetHashCode();
                                            if (!DeskManager.item_detail_data_dic.ContainsKey(itemData.id))
                                                DeskManager.item_detail_data_dic.Add(itemData.id, itemData);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                logger.LogWarning($"Skipping invalid file: Mod/{str}");
                            }
                        }
                    }
                }
            }
        }

        // Append Motions, never saw this one used, but you never know.
        internal static void AppendMotionData()
        {
            //string[] PhotoMotNei = null;
            //PhotoMotNei = GameUty.FileSystemMod.GetList("PhotMot_NEI", AFileSystemBase.ListType.AllFile);
            // Fix attempt, since 2.38 GetFile doesn't filter by folder
            IEnumerable<string> PhotoMotNei = ModPriority.modCache.Where(p => p.Contains("PhotMot_NEI")).Select(f => Path.GetFileName(f));

            foreach (string str in PhotoMotNei)
            {
                string nei_filename = Path.GetFileName(str);

                if (Path.GetExtension(nei_filename) == ".nei" && nei_filename != "phot_motion_list.nei")
                {
                    using (AFileBase aFileBase = GameUty.FileSystem.FileOpen(nei_filename))
                    {
                        using (CsvParser csvParser = new CsvParser())
                        {
                            if (csvParser.Open(aFileBase))
                            {
                                for (int i = 1; i < csvParser.max_cell_y; i++)
                                {
                                    int num = 0;
                                    PhotoMotionData photoMotionData = new PhotoMotionData();
                                    photoMotionData.id = csvParser.GetCellAsInteger(num++, i);
                                    photoMotionData.category = csvParser.GetCellAsString(num++, i);
                                    photoMotionData.name = csvParser.GetCellAsString(num++, i);
                                    photoMotionData.direct_file = csvParser.GetCellAsString(num++, i);
                                    photoMotionData.is_loop = csvParser.GetCellAsString(num++, i) == "○";
                                    photoMotionData.call_script_fil = csvParser.GetCellAsString(num++, i);
                                    photoMotionData.call_script_label = csvParser.GetCellAsString(num++, i);
                                    photoMotionData.is_mod = false;
                                    string cellAsString = csvParser.GetCellAsString(num++, i);
                                    bool flag = csvParser.GetCellAsString(num++, i) == "○";
                                    photoMotionData.use_animekey_mune_l = photoMotionData.use_animekey_mune_r = flag;
                                    photoMotionData.is_man_pose = csvParser.GetCellAsString(num++, i) == "○";
                                    PhotoMotionData.data.Add(photoMotionData);
                                }
                            }
                            else
                                logger.LogWarning($"Skipping invalid file: Mod/{str}");
                        }
                    }
                }

            }
        }
    }

    internal class NeiManagerPatches
    {
        [HarmonyPatch(typeof(PhotoBGData), nameof(PhotoBGData.Create))]
        [HarmonyPostfix]
        private static void PhotoBGData_Create_Postfix()
        {
            NeiManager.AppendBG();
        }
        
        
        [HarmonyPatch(typeof(PhotoBGObjectData), nameof(PhotoBGObjectData.Create))]
        [HarmonyPostfix]
        private static void PhotoBGObjectData_Create_Postfix()
        {
            NeiManager.AppendBGObject();
        }

        [HarmonyPatch(typeof(DeskManager), nameof(DeskManager.CreateCsvData))]
        [HarmonyPrefix]
        private static bool DeskManager_CreateCsvData_Prefix()
        {
            NeiManager.AppendDeskData();
            return true;
        }

        [HarmonyPatch(typeof(PhotoMotionData), nameof(PhotoMotionData.Create))]
        [HarmonyPostfix]
        private static void PhotoMotionData_Create_Postfix()
        {
            NeiManager.AppendMotionData();
        }
    }
}
