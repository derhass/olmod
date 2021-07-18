using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

/* NOTE: This class is meant as the central place for "cleaning up" server
 *       behavior, mainly by disabling stuff which is only neccessary on
 *       the client.
 *
 *       Currently, it only disables audio playback on the server, but
 *       I plan to add further patches later.
 */
namespace GameMod {
    [HarmonyPatch(typeof(GameManager), "Awake")]
    class ServerCleanup_GamaManager_Awake {
        static void Postifx() {
            if (GameplayManager.IsDedicatedServer()) {
                AudioListener.pause = true;
                Debug.Log("Dediacted Server: paused AudioListener");
            }
        }
    }

    /* enable this to hear the server's audio... 
    [HarmonyPatch(typeof(GameManager), "Update")]
    class ServerCleanup_GamaManager_OverrideAudioVolume {
        static void Postfix() {
            if (GameplayManager.IsDedicatedServer()) {
                AudioListener.volume=1.0f;
            }
        }
    }
    */

    [HarmonyPatch(typeof(UnityAudio), "FindNextOpenAudioSlot")]
    class ServerCleanup_NoOpenAudioSlot {
        static bool Prefix(ref int __result) {
            // tell the server we are sorry, but we don't have any audio channels left...
            if (GameplayManager.IsDedicatedServer()) {
                __result = -1;
                //Debug.Log("XXXXX audio playback suppressed on server");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityAudio), "PlayMusic")]
    class ServerCleanup_NoPlayMusic {
        static bool Prefix() {
            // suppress PlayMusic requests on the server...
            if (GameplayManager.IsDedicatedServer()) {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityAudio), "UpdateAudio")]
    class ServerCleanup_NoUpdateAudio {
        static bool Prefix() {
            // don't do UpdateAudio on the Server
            if (GameplayManager.IsDedicatedServer()) {
                return false;
            }
            return true;
        }
    }

    public class Foo {
        public static void SetFixedDeltaTime(float v)
        {
	        Debug.LogFormat("fixed delta time from {0} to {1}", Time.fixedDeltaTime, v);
	        if (Time.fixedDeltaTime != v) {
               Time.fixedDeltaTime = v;
               Debug.Log("did it!");
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "Update")]
    class ServerCleanup_XXX {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int i=0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call &&  ((MethodInfo)code.operand).Name == "set_fixedDeltaTime")
                {
                    Debug.LogFormat("patching set_fixedDeltaTime {0}", ++i);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Foo), "SetFixedDeltaTime"));

                    continue;
                }
                yield return code;
            }
        }
    }
}
