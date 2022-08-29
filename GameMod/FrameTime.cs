using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(PlayerShip), "Awake")]
    class FrameTimeInit
    {
        private static void Postfix()
        {
            GameManager.m_display_fps = Menus.mms_show_framerate;
        }
    }

    // fix the FPS calculation
    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    class FixFPSCalculation
    {
        private static uint lastFrameCount = 0;
        private static float lastFrameTime = 0.0f;
        private static float currentFPS = 0.0f;
        private static MethodInfo our_Method = AccessTools.Method(typeof(FixFPSCalculation), "CalculateFPS");

        private static float CalculateFPS()
        {
           float now = Time.realtimeSinceStartup;
           float duration = now - lastFrameTime;
           if (duration >= 0.25f) {
               uint frameCount = (uint)Time.frameCount;
               currentFPS = (frameCount - lastFrameCount) / duration;
               lastFrameCount = frameCount;
               lastFrameTime = now;
           }
           return currentFPS;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;

            foreach (var code in codes)
            {
                bool emitInstruction = true;
                //Debug.LogFormat("YYTS: {0} {1} {2}",state,code.opcode,code.operand);
                if (state == 0) {
                    /// Search the start of the average_fps calculation
                    if (code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "average_fps") {
                        state = 1;
                        emitInstruction = false;
                    }
                } else if (state == 1) {
                    // Search the end of the average_fps calculation
                    if (code.opcode == OpCodes.Add) {
                        state = 2;
                        // call our calculation method instead
                        yield return new CodeInstruction(OpCodes.Call, our_Method);
                    }
                    // omit the original calculation code
                    emitInstruction = false;
                }
                if (emitInstruction) {
                    yield return code;
                }
            }
            if (state != 2) {
                Debug.LogFormat("FixFPSCalculation: transpiler failed at state {0}",state);
            }
        }
    }
}
