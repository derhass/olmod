using HarmonyLib;
using Overload;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod.VersionHandling
{
       
   /* 
    [HarmonyPatch(typeof(GameplayManager), "Initialize")]
    class PCheckNewOlmodVersion
    {
        static void Postfix()
        {
            OlmodVersion.TryRefreshLatestKnownVersion();
        }
    }

    // Display olmod related information on main menu
    [HarmonyPatch(typeof(UIElement), "DrawMainMenu")]
    class PVersionDisplay
    {
        static string GetVersion(string stockVersion)
        {
            return $"{stockVersion} {OlmodVersion.FullVersionString.ToUpperInvariant()}";
        }

        // append olmod version to the regular version display on the main menu
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var _string_Format_Method = AccessTools.Method(typeof(String), "Format", new Type[] { typeof(string), typeof(object), typeof(object), typeof(object) });
            var _versionPatch_GetVersion_Method = AccessTools.Method(typeof(PVersionDisplay), "GetVersion");

            int state = 0;

            foreach (var code in codes)
            {
                // this.DrawStringSmall(string.Format(Loc.LS("VERSION {0}.{1} BUILD {2}"), GameManager.Version.Major, GameManager.Version.Minor, GameManager.Version.Build), position, 0.5f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == _string_Format_Method)
                {
                    state = 1;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, _versionPatch_GetVersion_Method);
                    continue;
                }

                yield return code;
            }
        }


        // Draw olmod modified label and olmod update button, if applicable
        static void Postfix(UIElement __instance)
        {

            Vector2 pos = new Vector2(UIManager.UI_RIGHT - 10f, -155f - 60f + 50f + 40f);
            __instance.DrawStringSmall("UNOFFICIAL MODIFIED VERSION", pos,
                0.35f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);
            
            // notify the player when a newer olmod verision is available
            if (OlmodVersion.RunningVersion < OlmodVersion.LatestKnownVersion)
            {
                pos = new Vector2(UIManager.UI_RIGHT - 10f, -155f - 60f + 50f + 60f);
                __instance.DrawStringSmall("OLMOD UPDATE AVAILABLE", pos,
                    0.35f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);

                __instance.SelectAndDrawHalfItem($"GET OLMOD {OlmodVersion.LatestKnownVersion}", new Vector2(UIManager.UI_RIGHT - 140f, 279f), 12, false);
            }
        }
    }

    // On select, get latest olmod version from main menu
    [HarmonyPatch(typeof(MenuManager), "MainMenuUpdate")]
    class PHandleOlmodUpdateSelect
    {
        private static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE && !NetworkManager.IsHeadless() && UIManager.PushedSelect(-1))
            {
                if (UIManager.m_menu_selection == 12)
                {
                    Application.OpenURL(OlmodVersion.NewVersionReleasesUrl);
                    MenuManager.PlayCycleSound(1f, 1f);
                }
            }
        }
    }
*/
    [HarmonyPatch(typeof(GameManager), "Awake")]
    class XXX
    {
        public static IEnumerator FooCoroutine()
        {
                var foo = AccessTools.Method(typeof(GameManager), "GetLatestVersionNumber");
                Debug.Log("XXXXXXXXXXXXXXXXXXXX FOOOOOOOOOOOOOOOOOOOOOOOOOO");
                IEnumerator x = (IEnumerator)foo.Invoke(GameManager.m_gm,null);
                Debug.LogFormat("XXXXXXXXXXXXXXXXXXXX {0} {1}",x, x.Current);
                yield return x.Current;
                while (x.MoveNext()) {
                  Debug.LogFormat("XXXXXXXXXXXXXXXXXXXX {0} {1}",x, x.Current);
                  yield return x.Current;
                }
                Debug.Log("XXXXXXXXXXXXXXXXXXXX FOOOOOOOOOOOOOOOOOOOOOOOOOO2");
        }

	public static Assembly GetOverloadAssembly()
	{
		return Assembly.GetAssembly(typeof(Overload.GameManager));
	}

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            // This patches the next call of Server.IsDedicatedServer() call after
            // a StartCoroutine was called to just pushing true on the stack,
            // which makes the Cheat Detection stuff being skipped also on the client
            int state = 0;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call) {
		       Debug.LogFormat("XXXXX {0}", ((MethodInfo)code.operand).Name);
		}
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "GetExecutingAssembly") {
                    var foo = AccessTools.Method(typeof(XXX), "GetOverloadAssembly");
                    yield return new CodeInstruction(OpCodes.Call,foo);
                  Debug.Log("XXXX ASSSS");
                    continue;
                }
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "GetLatestVersionNumber") {
                    var foo = AccessTools.Method(typeof(XXX), "FooCoroutine");
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Call,foo);
                    continue;
                }
                if (state == 0 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "StartCoroutine") {
                    state =1;
                } else if (state == 1 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "IsDedicatedServer") {
                  yield return new CodeInstruction(OpCodes.Ldc_I4_1); // push true on the stack instead
                  Debug.Log("XXXX bypass injection detection");
                  state = 2;
                  continue;
                }

                yield return code;
            }
        }

        private static void Prefix()
        {
        }
        private static void Postfix()
        {
		Debug.LogFormat("XXX {0}",GameManager.Version);
        }
    }
}
   /* */
/*
    [HarmonyPatch(typeof(GameManager), "GetLatestVersionNumber")]
    class XXY
    {
        private static void Prefix()
        {
                UnityEngine.Debug.Log("XXXXXXXXXXXXXX STARTED");
        }
        private static void Postfix()
        {
                UnityEngine.Debug.Log("XXXXXXXXXXXXXX DONE");
        }
    }*/

