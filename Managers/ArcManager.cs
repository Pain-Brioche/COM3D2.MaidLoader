using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using BepInEx.Logging;
using CM3D2.Toolkit.Guest4168Branch.Arc;
using CM3D2.Toolkit.Guest4168Branch.Arc.Entry;
using CM3D2.Toolkit.Guest4168Branch.Arc.FilePointer;
using HarmonyLib;
using ShortStartLoader;

namespace COM3D2.MaidLoader
{
    internal class ArcManager
    {
        private static ManualLogSource logger = MaidLoader.logger;
        private static Harmony harmony;

        private static string gamePath = UTY.gameProjectPath;
        private static string modPath = Path.Combine(gamePath, "Mod");
        private static string gamedataPath = Path.Combine(gamePath, "GameData");
        private static string dummyArc = "MaidLoader";

        private static bool loadKS = MaidLoader.loadScripts.Value;
        private static bool loadSounds = MaidLoader.loadSounds.Value;
        private static bool useDedicatedSSFolder = MaidLoader.useDedicatedSSFolder.Value;
        private static bool loadArc = false;

        private static List<string> files = new();
        private static List<string> arcList = new();



        internal static void Init()
        {
            //Look for any .arc in the mod folder
            if (loadArc) { arcList = GetArc(); }

            if (useDedicatedSSFolder & !Directory.Exists(modPath + "\\Scripts&Sounds"))
                Directory.CreateDirectory(modPath + "\\Scripts&Sounds");

            if (loadKS || loadSounds)
            {
                Stopwatch sw = Stopwatch.StartNew();
                sw.Start();

                logger.LogInfo("Looking for files to load");

                //Look for any .ks in the mod folder
                if (loadKS)
                    files.AddRange(GetScripts());

                //Look for any .ogg in the mod folder
                if (loadSounds)
                    files.AddRange(GetSounds());

                sw.Stop();
                logger.LogInfo($"Found {files.Count} file(s) in {sw.ElapsedMilliseconds}ms.");
            }

            // No need load the dummy .arc if there's nothing to add.
            if (files.Count > 0)
            {
                //Only Patch what's needed
                if (!MaidLoader.SSL)
                {
                    harmony = Harmony.CreateAndPatchAll(typeof(Patchers));
                }
                else
                {
                    harmony = Harmony.CreateAndPatchAll(typeof(SSLPatcher));
                }

                //Build the Dummy .arc
                BuildArc();
            }
        }

        /// <summary>
        /// Returns an edited block of IL code with additional .arc to load
        /// </summary>
        /// <param name="rawIL">IL Harmony sends</param>
        /// <param name="arcList">.arc found in the mod folder</param>
        /// <param name="addCustomArc">add a custom .arc for special files</param>
        /// <returns></returns>
        internal static List<CodeInstruction> GetEdited(List<CodeInstruction> rawIL,string lastArc = "parts2", bool addCustomArc = true)
        {
            var ILBlock = new List<CodeInstruction>();
            var editedIL = new List<CodeInstruction>(rawIL);

            if (addCustomArc)
            {
                arcList.Add(dummyArc);
            }

            // Itterate through the IL code Harmony sent
            for (int i = 0; i < rawIL.Count; i++)
            {
                // Look for "parts2" latest .arc loaded by the game at any time.
                if (rawIL[i].operand as string == lastArc)
                {

                    // make a line IL block for each .arc to load.
                    foreach (string arc in arcList)
                    {
                        //Main.logger.LogMessage($"{arc}.arc found and will be loaded");
                        var newIL = new List<CodeInstruction>();

                        //Make it a deep copy of the part we want to edit
                        newIL = rawIL.GetRange(i - 2, 5).ConvertAll(il => il.Clone());

                        //Change parts2 with out custom .arc
                        newIL[2].operand = arc;

                        ILBlock.AddRange(newIL);
                    }

                    //Inject that bit of IL back
                    editedIL.InsertRange(i + 3, ILBlock);

                    /*
                    for (int k = 0; k < editedIL.Count; k++)
                    {
                        logger.LogWarning($"New IL: {editedIL[k].opcode} {editedIL[k].operand}");
                    }
                    */

                    return editedIL;
                }
            }
            return rawIL;
        }

        /// <summary>
        /// Build a dummy .arc to store files into
        /// </summary>
        private static void BuildArc()
        {
            // Creating a new dummy .arc like ModLoader used to do.
            ArcFileSystem arc = new();


            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                ArcFileEntry arcFile = !arc.FileExists(file) ? arc.CreateFile(fileName) : arc.GetFile(file);
                arcFile.Pointer = new WindowsFilePointer(file);
            }

            // saving the dummy .arc
            string arcName = dummyArc + ".arc";
            try
            {
                using FileStream fStream = File.Create(Path.Combine(gamedataPath, arcName));
                arc.Save(fStream);
            }
            catch (IOException)
            {
                logger.LogWarning($"{dummyArc}.arc can't be accessed, eventual new files won't be added");
            }
        }

        /// <summary>
        /// Returns all .ks found in the Mod folder
        /// </summary>
        private static List<string> GetScripts()
        {
            List<string> scripts = new List<string>();

            if (useDedicatedSSFolder)
                scripts = Directory.GetFiles(Path.Combine(gamePath, "Scripts&Sounds"), "*.ks", SearchOption.AllDirectories).ToList();                
            else
                scripts = Directory.GetFiles(modPath, "*.ks", SearchOption.AllDirectories).ToList();            

            return scripts;
        }

        /// <summary>
        /// Returns all .ogg found in the Mod folder
        /// </summary>
        private static List<string> GetSounds()
        {
            List<string> sounds = new List<string>();

            if (useDedicatedSSFolder)
                sounds = Directory.GetFiles(Path.Combine(gamePath, "Scripts&Sounds"), "*.ogg", SearchOption.AllDirectories).ToList();
            else
                sounds = Directory.GetFiles(modPath, "*.ogg", SearchOption.AllDirectories).ToList();

            return sounds;
        }

        /// <summary>
        /// Returns all .arc found in the Mod folder
        /// </summary>
        private static List<string> GetArc()
        {
            List<string> result = new();

            foreach (string arc in Directory.GetFiles(modPath, "*.arc", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(arc);
                //Main.logger.LogMessage($"Found: {fileName}.arc");
                result.Add(fileName);
            }

            return result;
        }
    }

    internal class Patchers
    {
        // Add intructions to load the custom .arc alongside regular .arc
        [HarmonyPatch(typeof(GameUty), nameof(GameUty.UpdateFileSystemPath))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UpdateFileSystemPath_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            //Returns an edited block of IL code with additional .arc to load
            codes = ArcManager.GetEdited(codes, "parts2");

            return codes;
        }

        // Same thing for the English release
        [HarmonyPatch(typeof(GameUty), nameof(GameUty.UpdateFileSystemPathToNewProduct))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UpdateFileSystemPathToNewProduct_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            //Returns an edited block of IL code with additional .arc to load
            codes = ArcManager.GetEdited(codes, "parts-en2");

            return codes;
        }
    }

    internal class SSLPatcher
    {
        // Still the same thing for ShortStartLoader this time
        [HarmonyPatch(typeof(MainBigRedo), nameof(MainBigRedo.UpdateFileSystemPath))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> UpdateFileSystemPathSSL_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            //Returns an edited block of IL code with additional .arc to load
            codes = ArcManager.GetEdited(codes, "parts2");

            return codes;
        }
    }
}
