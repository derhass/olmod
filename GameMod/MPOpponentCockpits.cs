using System;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public class MPOpponentCockpits {
        public static void SetOpponentCockpitVisibility(Player p, bool enabled) {
            if (p != null && p.c_player_ship != null && !p.isLocalPlayer && !p.m_spectator) {
                //Debug.LogFormat("Setting cockpit visibility for player ship {0} to {1}",p.m_mp_name, enabled);
                MeshRenderer[] componentsInChildren = p.c_player_ship.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                foreach (MeshRenderer meshRenderer in componentsInChildren)
                {
                    if (meshRenderer.enabled != enabled) {
                        if (string.CompareOrdinal(meshRenderer.name, 0, "cp_", 0, 3) == 0) {
                            meshRenderer.enabled = enabled;
                            meshRenderer.shadowCastingMode = 0;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Overload.PlayerShip), "SetCockpitVisibility")]
        class MPOpponentCockpits_Disable1 {
            static void Postfix(PlayerShip __instance) {
                SetOpponentCockpitVisibility(__instance.c_player, false);
            }
        }

        [HarmonyPatch(typeof(Overload.Player), "RestorePlayerShipDataAfterRespawn")]
        class MPOpponentCockpits_Disable2 {
            static void Postfix(Player __instance) {
                SetOpponentCockpitVisibility(__instance, false);
            }
        }

        public static void SetOpponentVisibility(Player p, int cockpit, int rest) {
            if (p != null && p.c_player_ship != null && !p.isLocalPlayer && !p.m_spectator) {
                Debug.LogFormat("iSetVis: {0} {1} {2}",p.m_mp_name,cockpit,rest);
                MeshRenderer[] componentsInChildren = p.c_player_ship.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                foreach (MeshRenderer meshRenderer in componentsInChildren)
                {
                    bool isCockpit = false;
                    if (string.CompareOrdinal(meshRenderer.name, 0, "cp_", 0, 3) == 0) {
                        isCockpit = true;
                    }
                    int mode = (isCockpit)?cockpit:rest;
                    if (mode == 0) {
                        meshRenderer.enabled = false;
                    } else if (mode > 0) {
                        meshRenderer.enabled = true;
                    }
                }
            }
        }

        private static void hack_cockpit_command() {
                int n = uConsole.GetNumParameters();
                int v = 1;
                int w = 1;
                if (n > 0) {
                        v = uConsole.GetInt();
                }
                if (n > 1) {
                        w = uConsole.GetInt();
                }
                foreach (Player p in NetworkManager.m_Players) {
                    SetOpponentVisibility(p,v,w);
                }
        }

        [HarmonyPatch(typeof(GameManager), "Awake")]
        class MPErrorSmoothingFix_Controller {
            static void Postfix() {
                uConsole.RegisterCommand("hack_vis", hack_cockpit_command);
            }
        }
    }
}
