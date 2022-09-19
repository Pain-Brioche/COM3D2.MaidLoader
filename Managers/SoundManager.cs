using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.MaidLoader.Managers
{
    internal class SoundManager
    {
        private static Harmony harmony;

        internal static void Init()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(SoundManager));
        }

        // Attempt at loading .ogg directly from the Mod folder without creating a dummy .arc

        [HarmonyPatch(typeof(AudioSourceMgr), nameof(AudioSourceMgr.LoadPlay))]
        [HarmonyPrefix]
        public static bool LoadPlayPrefix(AudioSourceMgr __instance, string f_strFileName, float f_fFadeTime, bool f_bStreaming, bool f_bLoop = false)
        {
            if (string.IsNullOrEmpty(f_strFileName))
            {
                Debug.LogWarning("Sound file name is empty. " + f_strFileName);
                return false; ;
            }
            AFileSystemBase[] array;
            if (__instance.m_eType == AudioSourceMgr.Type.Voice || __instance.m_eType == AudioSourceMgr.Type.VoiceHeroine || __instance.m_eType == AudioSourceMgr.Type.VoiceSub || __instance.m_eType == AudioSourceMgr.Type.VoiceExtra || __instance.m_eType == AudioSourceMgr.Type.VoiceMob)
            {
                if (__instance.m_gcSoundMgr.compatibilityMode)
                {
                    array = new AFileSystemBase[]
                    {
                        GameUty.FileSystemMod,
                        GameUty.FileSystemOld,
                        GameUty.FileSystem
                    };
                }
                else
                {
                    array = new AFileSystemBase[]
                    {
                        GameUty.FileSystemMod,
                        GameUty.FileSystem,
                        GameUty.FileSystemOld
                    };
                }
            }
            else if (__instance.m_gcSoundMgr.compatibilityMode)
            {
                array = new AFileSystemBase[]
                {
                    GameUty.FileSystemMod,
                    GameUty.FileSystemOld
                };
            }
            else
            {
                array = new AFileSystemBase[]
                {
                    GameUty.FileSystemMod,
                    GameUty.FileSystem
                };
            }
            AFileSystemBase afileSystemBase = null;
            foreach (AFileSystemBase afileSystemBase2 in array)
            {
                if (afileSystemBase2.IsExistentFile(f_strFileName))
                {
                    afileSystemBase = afileSystemBase2;
                    break;
                }
            }
            if (afileSystemBase == null)
            {
                Debug.LogWarning("Sound file not found. " + f_strFileName);
                return false;
            }
            __instance.m_bDanceBGM = false;
            if (__instance.audiosource.outputAudioMixerGroup == __instance.m_gcSoundMgr.mix_mgr[AudioMixerMgr.Group.Dance])
            {
                __instance.audiosource.outputAudioMixerGroup = __instance.m_gcSoundMgr.mix_mgr[AudioMixerMgr.Group.BGM];
            }
            __instance.LoadFromWf(afileSystemBase, f_strFileName, f_bStreaming);
            __instance.Play(f_fFadeTime, f_bLoop);

            return false;
        }
    }
}
