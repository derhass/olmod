using Harmony;
using Overload;
using System.Collections.Generic;
using UnityEngine;

namespace GameMod {
    public class MPErrorSmoothingFix {
        private static Vector3    lastPosition = new Vector3();
        private static Quaternion lastRotation = new Quaternion();
        private static Vector3    currPosition = new Vector3();
        private static Quaternion currRotation = new Quaternion();
        private static Vector3    savedLocalPosition = new Vector3();
        private static Quaternion savedLocalRotation = new Quaternion();
        private static bool       doManualInterpolation = false;
        private static bool       localTransformOverridden = false;
        private static Transform  localTransformNode = null;

        private static int hackIsEnabled = 0;

        private static void dump_transform(int level, Transform t)
        {
                UnityEngine.Debug.LogFormat("{0}: {1} Lp ({2} {3} {4}) Lr ({5} {6} {7}) Ls ({8} {9} {10})",
                                level, t.name, t.localPosition.x, t.localPosition.y, t.localPosition.z,
                                t.localEulerAngles.x, t.localEulerAngles.y, t.localEulerAngles.z,
                                t.localScale.x, t.localScale.y, t.localScale.z);

        }

        private static void dump_transform_hierarchy(Transform t)
        {
                int level = 0;
                while (t != null) {
                        dump_transform(level, t);
                        t=t.parent;
                        level++;
                }
        }
        private static void hack_smooth_command() {
                int n = uConsole.GetNumParameters();
                if (n > 0) {
                        int value = uConsole.GetInt();
                        hackIsEnabled = value;
                } else {
                        hackIsEnabled = (hackIsEnabled >0)?0:1;
                }
                UnityEngine.Debug.LogFormat("hack_smooth is now {0}", hackIsEnabled);
        }
        [HarmonyPatch(typeof(PlayerShip), "Awake")]
        class MPErrorSmoothingFix_X1 {
            static void Postfix() {
               // GameManager.m_local_player.c_player_ship.c_rigidbody.interpolation=RigidbodyInterpolation.None;
            }
        }

        [HarmonyPatch(typeof(GameManager), "Awake")]
        class MPErrorSmoothingFix_Controller {
            static void Postfix() {
                uConsole.RegisterCommand("hack_smooth", hack_smooth_command);
            }
        }

        [HarmonyPatch(typeof(GameManager), "FixedUpdate")]
        class MPErrorSmoothingFix_UpdateCycle {
            static void Prefix() {
                if (localTransformOverridden) {
                    localTransformNode.localPosition = savedLocalPosition;
                    localTransformNode.localRotation = savedLocalRotation;
                    localTransformOverridden = false;
                }
            }
            static void Postfix() {
                // only on the Client, in Multiplayer, in an active game, not during death roll:
                if ((hackIsEnabled>0) && !Server.IsActive() && GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && !GameManager.m_local_player.c_player_ship.m_dying) {
                    if (!doManualInterpolation) {
                        GameManager.m_local_player.c_player_ship.c_rigidbody.interpolation = RigidbodyInterpolation.None;
                        localTransformNode = GameManager.m_local_player.c_player_ship.c_camera.transform;
                        doManualInterpolation = true;
                        currPosition = localTransformNode.position;
                        currRotation = localTransformNode.rotation;
                    }
                    lastPosition = currPosition;
                    lastRotation = currRotation;
                    currPosition = localTransformNode.position;
                    currRotation = localTransformNode.rotation;
                } else {
                    if (doManualInterpolation) {
                      doManualInterpolation = false;
                      localTransformNode = null;
                      GameManager.m_local_player.c_player_ship.c_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        class MPErrorSmoothingFix_FixedUpdateCycle {
            static void Prefix() {
                if (localTransformOverridden) {
                    localTransformNode.localPosition = savedLocalPosition;
                    localTransformNode.localRotation = savedLocalRotation;
                    localTransformOverridden = false;
                }
            }
            static void Postfix() {
                if (doManualInterpolation && (localTransformNode != null)) {
                    UnityEngine.Debug.Log("XXX BEFORE");
                    dump_transform_hierarchy(GameManager.m_local_player.c_player_ship.c_camera.transform);
                    savedLocalPosition = localTransformNode.localPosition;
                    savedLocalRotation = localTransformNode.localRotation;
                    float fract = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
                    UnityEngine.Debug.LogFormat("STEP: fract: {0}", fract);
                    localTransformNode.position = Vector3.LerpUnclamped(lastPosition, currPosition, fract);
                    localTransformNode.rotation = Quaternion.SlerpUnclamped(lastRotation, currRotation, fract);
                    localTransformOverridden = true;
                    UnityEngine.Debug.Log("XXX After");
                    dump_transform_hierarchy(GameManager.m_local_player.c_player_ship.c_camera.transform);
                }
            }
        }
    }
}
