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
using System.Threading.Tasks;
using System.Reflection.Emit;

namespace COM3D2.MaidLoader
{
    internal class ArcManager
    {
        private static ManualLogSource logger = MaidLoader.logger;
        private static Harmony harmony;
        private static Harmony harmonySSL;

        private static readonly string gamePath = UTY.gameProjectPath;
        private static readonly string modPath = Path.Combine(gamePath, "Mod");
        //private static readonly string gamedataPath = Path.Combine(gamePath, "GameData");
        private static readonly string dummyArc = "MaidLoader.arc";

        private static bool loadArc = MaidLoader.loadArc.Value;
        private static bool loadKS = MaidLoader.loadScripts.Value;
        private static bool loadSounds = MaidLoader.loadSounds.Value;
        private static bool useDedicatedSSFolder = MaidLoader.useDedicatedSSFolder.Value;

        private static List<string> arcList = new();
        private static Task buildArc;

        internal ArcManager()
        {
            string[] files = new string[0];
            Stopwatch sw = Stopwatch.StartNew();


            //Look for .ks/.ogg in either the entire Mod folder or a dedicated one.
            if (loadKS || loadSounds)
            {
                sw.Start();

                if (useDedicatedSSFolder & !Directory.Exists(modPath + "\\Scripts&Sounds"))
                    Directory.CreateDirectory(modPath + "\\Scripts&Sounds");                

                logger.LogInfo("Looking for .ks/.ogg");

                string searchPath = useDedicatedSSFolder ? Path.Combine(modPath, "Scripts&Sounds") : modPath;

                if (loadKS && !loadSounds)
                    files = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories).Where(f => f.ToLower().EndsWith(".ks")).ToArray();
                else if (!loadKS && loadSounds)
                    files = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories).Where(f => f.ToLower().EndsWith(".ogg")).ToArray();
                else
                    files = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories).Where(f => f.ToLower().EndsWith(".ks") || f.ToLower().EndsWith(".ogg")).ToArray();

                sw.Stop();
                logger.LogInfo($"Found {files.Length} file(s) in {sw.ElapsedMilliseconds}ms.");
            }

            //Look for .arc in the entire Mod folder.
            if (loadArc)
            {
                sw.Reset();
                sw.Start();
                logger.LogInfo("Looking for .arc");

                arcList.AddRange(Directory.GetFiles(modPath, "*.*", SearchOption.AllDirectories).Where(f => f.ToLower().EndsWith(".arc")));

                sw.Stop();
                logger.LogInfo($"Found {arcList.Count} file(s) in {sw.ElapsedMilliseconds}ms.");
            }

            //Only Patch what's needed when needed
            if (arcList.Count > 0 || files.Length > 0)
            {
                if (MaidLoader.SSL)
                {
                    logger.LogError("Patching SSL");
                    harmonySSL = Harmony.CreateAndPatchAll(typeof(SSLPatcher));
                }
                logger.LogError("Patching Game");
                harmony = Harmony.CreateAndPatchAll(typeof(Patchers));
            }

            // No need make the dummy.arc if there's nothing to add.
            if (files.Length > 0)
            {
                //Build the Dummy .arc in a separate thread
                buildArc = Task.Factory.StartNew(() =>
                {
                    BuildArc(files);
                });
            }            
        }


        /// <summary>
        /// Build a dummy .arc to store files into
        /// </summary>
        private void BuildArc(string[] files)
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
            string arcName = dummyArc;
            try
            {
                using FileStream fStream = File.Create(Path.Combine(BepInEx.Paths.CachePath, dummyArc));
                arc.Save(fStream);
            }
            catch (IOException)
            {
                logger.LogWarning($"{dummyArc} can't be accessed, eventual new files won't be added");
            }
        }

        private static void LoadArc()
        {
            //Add out dummy .arc
            arcList.Add(Path.Combine(BepInEx.Paths.CachePath, dummyArc));

            bool isAnnounced = false;

            //wait for the dummy .arc to be built
            while (!buildArc.IsCompleted) 
            {
                if (!isAnnounced)
                    logger.LogInfo("Waiting for MaidLoader.arc to be created");

                isAnnounced = true;
                System.Threading.Thread.Sleep(50);               
            }

            // load each .arc in the list.
            foreach (string arc in arcList)
            {
                FileSystemArchive gameFileSystem = GameUty.FileSystem as FileSystemArchive;

                gameFileSystem.AddArchive(arc);
                logger.LogInfo($"■■■■■■■ [{Path.GetFileName(arc)}] Loaded.");
                GameUty.loadArchiveList.Add(Path.GetFileNameWithoutExtension(arc).ToLower());
            }
        }

        internal class Patchers
        {
            // Add intructions to load the custom .arc alongside regular .arc
            [HarmonyPatch(typeof(GameUty), nameof(GameUty.UpdateFileSystemPath))]
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> UpdateFileSystemPath_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                //Initial checkpoint puts us where we want
                var checkpoint = new CodeMatcher(instructions)
                    .MatchForward(true,
                        new CodeMatch(l => l.opcode == OpCodes.Ldstr && (l.operand as string).Equals("parts2"))
                    );

                //Inject our method call~
                var result =
                    checkpoint
                    .Advance(3)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArcManager), "LoadArc")))
                    .InstructionEnumeration();

                return result;
            }
            
            
            // Same thing for the English release
            [HarmonyPatch(typeof(GameUty), nameof(GameUty.UpdateFileSystemPathToNewProduct))]
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> UpdateFileSystemPathToNewProduct_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                //Initial checkpoint puts us where we want
                var checkpoint = new CodeMatcher(instructions)
                    .MatchForward(true,
                        new CodeMatch(l => l.opcode == OpCodes.Ldstr && (l.operand as string).Equals("parts-en2"))
                    );

                //Inject our method call~
                var result =
                    checkpoint
                    .Advance(3)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArcManager), "LoadArc")))
                    .InstructionEnumeration();

                return result;
            }            
        }

        internal class SSLPatcher
        {
            // Still the same thing for ShortStartLoader this time
            [HarmonyPatch(typeof(StartupOptimize), nameof(StartupOptimize.UpdateFileSystemPath))]
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> UpdateFileSystemPathSSL_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var checkpoint = new CodeMatcher(instructions)
                    .MatchForward(true,
                        new CodeMatch(l => l.opcode == OpCodes.Ldstr && (l.operand as string).Equals("parts2"))
                    );

                var result =
                    checkpoint
                    .Advance(3)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArcManager), "LoadArc")))
                    .InstructionEnumeration();

                return result;
            }
        }
    }
}
