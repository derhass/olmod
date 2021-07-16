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
        private static bool       doManualInterpolation = false;
        private static bool       targetTransformOverridden = false;
        private static Transform  targetTransformNode = null;

        private static int hackIsEnabled = 1;
        private static int hackError = 0;

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
        private static void hack_error_command() {
                int n = uConsole.GetNumParameters();
                if (n > 0) {
                        int value = uConsole.GetInt();
                        hackError = value;
                } else {
                        hackError = (hackError>0)?0:1;
                }
                UnityEngine.Debug.LogFormat("hack_error is now {0}", hackError);
        }

        // start the manual interpolation phase
        // this disables Unity's automatic interpolation for the rigid body of the player ship
        private static void enableManualInterpolation()
        {
            doManualInterpolation = true;
            targetTransformNode = GameManager.m_local_player.c_player_ship.transform;
            if (targetTransformNode) {
                GameManager.m_local_player.c_player_ship.c_rigidbody.interpolation = RigidbodyInterpolation.None;
                currPosition = targetTransformNode.position;
                currRotation = targetTransformNode.rotation;
            }
        }

        // stop the manual interpolation phase
        // restore Unity's automatic interpolation for the rigid body of the player ship
        private static void disableManualInterpolation()
        {
            doManualInterpolation = false;
            targetTransformNode = null;
            targetTransformOverridden = false;
            GameManager.m_local_player.c_player_ship.c_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // override the transformation of the target node
        // use smooth interpolation based on Time.time relative to Time.fixedTime
        // this should be done PER FRAME
        private static void doTransformOverride()
        {
            // interpolate the position
            float fract = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            if (hackIsEnabled > 1) {
                fract += 1.0f;
            }
            targetTransformNode.position = Vector3.LerpUnclamped(lastPosition, currPosition, fract);
            targetTransformNode.rotation = Quaternion.SlerpUnclamped(lastRotation, currRotation, fract);

            // mark the transformation as overridden
            targetTransformOverridden = true;
        }

        // undo the transformation we modified
        private static void undoTransformOverride()
        {
            if (targetTransformOverridden) {
                if (targetTransformNode != null) {
                    targetTransformNode.position = currPosition;
                    targetTransformNode.rotation = currRotation;
                }
                targetTransformOverridden = false;
            }
        }

        /*
        [HarmonyPatch(typeof(Player), "SetSmoothingErrorRot")]
        class MPErrorSmoothingFix_ErrRot {
            static bool Prefix() {
                if (hackError > 0) {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "SetSmoothingErrorPos")]
        class MPErrorSmoothingFix_ErrPos {
            static bool Prefix() {
                if (hackError > 0) {
                    return false;
                }
                return true;
            }
        }
        */
        [HarmonyPatch(typeof(Player), "AddSmoothingError")]
        class MPErrorSmoothingFix_ErrAdd {
            static bool Prefix() {
                if (hackError == 1) {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "RemoveSmoothingError")]
        class MPErrorSmoothingFix_ErrRemove {
            static bool Prefix() {
                if (hackError == 1) {
                    return false;
                }
                return true;
            }
        }


        [HarmonyPatch(typeof(GameManager), "Awake")]
        class MPErrorSmoothingFix_Controller {
            static void Postfix() {
                uConsole.RegisterCommand("hack_smooth", hack_smooth_command);
                uConsole.RegisterCommand("hack_error", hack_error_command);
            }
        }

        [HarmonyPatch(typeof(GameManager), "FixedUpdate")]
        class MPErrorSmoothingFix_UpdateCycle {
            static void Prefix() {
                // only on the Client, in Multiplayer, in an active game, not during death roll:
                if (!Server.IsActive() && GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && !GameManager.m_local_player.c_player_ship.m_dying) {
                    // undo potential override also before FixedUpdate
                    undoTransformOverride();
                }
            }

            static void Postfix() {
                // only on the Client, in Multiplayer, in an active game, not during death roll:
                if ( (hackIsEnabled > 0) && !Server.IsActive() && GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && !GameManager.m_local_player.c_player_ship.m_dying) {
                    if (!doManualInterpolation) {
                        enableManualInterpolation();
                    }
                    lastPosition = currPosition;
                    lastRotation = currRotation;
                    currPosition = targetTransformNode.position;
                    currRotation = targetTransformNode.rotation;
                } else if (doManualInterpolation) {
                    disableManualInterpolation();
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        class MPErrorSmoothingFix_FixedUpdateCycle {
            static void Prefix() {
                if (doManualInterpolation && (targetTransformNode != null)) {
                    doTransformOverride();
                }
            }
        }

        [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
        class MPErrorSmoothingFix_InitBeforeEachMatch {
            private static void Prefix() {
                disableManualInterpolation();
            }
        }
    }
}
