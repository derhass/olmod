using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading;
using GameMod.VersionHandling;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public class OnDemandHackErrorValue
    {
        private static bool initialized = false;
        public static string GetName()
        {
            return "ErrorValue";
        }

        public static void Start()
        {
            if (initialized) {
                Debug.LogFormat("ODMHack: {0} already enabled", GetName());
            } else {
                if (DoStart()) {
                    initialized = true;
                    Debug.LogFormat("ODMHack: {0} enabled", GetName());
                } else {
                    Debug.LogFormat("ODMHack: {0} FAILED", GetName());
                }
            }
        }

        private static bool DoStart()
        {
            Harmony harmony = OnDemandHacks.ourHarmony;
            harmony.Patch(AccessTools.Method(typeof(UIElement), "DrawPing"), null, new HarmonyMethod(typeof(OnDemandHackErrorValue).GetMethod("DrawPingPostfix"), Priority.Last));
            return true;
        }

        public static void DrawPingPostfix(Vector2 pos, UIElement __instance)
        {
            if (GameManager.m_local_player != null) {
                float posErr = GameManager.m_local_player.m_error_pos.magnitude;
                float rotErr = 0.0f;
                Vector3 axis = Vector3.zero;
                GameManager.m_local_player.m_error_rot.ToAngleAxis(out rotErr, out axis);
                Color c = Color.Lerp(UIManager.m_col_good_ping, UIManager.m_col_em5, (10.0f*posErr + rotErr) / 5.0f);
                pos.x -= 130f;
                __instance.DrawStringSmall(String.Format("{0,5:0.000} {1,5:0.000}", posErr, rotErr), pos, 0.4f, StringOffset.LEFT, c, 1f);
            }
        }
    }

    public class OnDemandHacks
    {
        public static Harmony ourHarmony = null;
        private static bool inited = false;

        // Initialize the OnDemand Hack code
        public static void Initialize(Harmony harmony)
        {
            if (!inited) {
                ourHarmony = harmony;
                harmony.Patch(AccessTools.Method(typeof(GameManager), "Start"), null, new HarmonyMethod(typeof(OnDemandHacks).GetMethod("StartPostfix"), Priority.Last));
                inited = true;
            }
        }

        // This is an additional Postfix to GameManager.Start() to register our console commands
        public static void StartPostfix()
        {
            uConsole.RegisterCommand("odmhack", "Enable On-Demand Hack [feature]", CmdOdmhack);
            uConsole.RegisterCommand("printcolliders", "print all player ships' collider types", CmdPrintColliders);
        }

        // Console command omdhack
        static void CmdOdmhack()
        {
            string feature = uConsole.GetString();
            if (String.IsNullOrEmpty(feature)) {
                Debug.Log("ODMHack: please speciy the feature to enable!");
                return;
            }

            switch(feature) {
                case "errorvalue": OnDemandHackErrorValue.Start(); break;
                default: Debug.LogFormat("ODMHack: {0} not found!",feature); return;
            }
        }

        // Console command printcolliders
        static void CmdPrintColliders()
        {
            foreach (var p in Overload.NetworkManager.m_Players) {
                Debug.LogFormat("{0}: {1} {2}", p.m_mp_name, p.c_player_ship.c_mesh_collider, p.c_player_ship.c_mesh_collider.enabled);
            }
        }
    }
}
