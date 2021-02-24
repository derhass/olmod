﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

// Commented out code represents an attempt at making ships respond to physics.  It didn't work, and will be revisited in the future.
namespace GameMod {
    //public class MPClientExtrapolation_Velocities {
    //    public Vector3 Velocity;
    //    public Vector3 AngularVelocity;
    //    public float Drag;
    //    public Vector3 LocalPosition;
    //    public Quaternion Rotation;
    //}

    public class MPClientExtrapolation {
        public const int MAX_PING = 1000;
        public static List<Rigidbody> bodies_to_resolve = new List<Rigidbody>();
        //        public static Dictionary<Player, MPClientExtrapolation_Velocities> players_to_resolve = new Dictionary<Player, MPClientExtrapolation_Velocities>();
        public static Dictionary<string, Queue<Vector3>> player_positions = new Dictionary<string, Queue<Vector3>>();

        public static void LerpProjectile(Projectile c_proj) {
            // Bail if:
            if (
                !GameplayManager.IsMultiplayerActive ||          // it's not a MP game (no network)
                Network.isServer ||                              // if it's the server (not necessary)
                MPObserver.Enabled ||                            // if the current player is an observer (not necessary for observer games)
                c_proj.m_owner_player.isLocalPlayer ||           // it's the local player (not necessary)
                c_proj.m_type == ProjPrefab.missile_creeper ||   // a creeper (handled by creeper/TB sync)
                c_proj.m_type == ProjPrefab.missile_timebomb ||  // a time bomb (handled by creeper/TB sync)
                c_proj.m_type == ProjPrefab.proj_flak_cannon ||  // a flak projectile (lifespan is too short to make a difference)
                c_proj.m_type == ProjPrefab.proj_driller ||      // a driller projectile (projectile is too fast to make a difference)
                c_proj.m_type == ProjPrefab.proj_driller_mini || // do not lerp projectile children
                c_proj.m_type == ProjPrefab.missile_smart_mini ||
                c_proj.m_type == ProjPrefab.missile_devastator_mini
            ) {
                return;
            }

            // Queue to simulate physics.
            bodies_to_resolve.Add(c_proj.c_rigidbody);
        }

        // Factor is 0 for observers, and the average of the desired 1.0 for ship positions and 0.5 for projectile positions for non-observers.
        public static float GetFactor() {
            return MPObserver.Enabled ? 0f : 0.75f;
        }

        // How far ahead to advance weapons, in seconds.
        public static float GetWeaponExtrapolationTime() {
            if (MPObserver.Enabled) {
                return 0f;
            }
            float time_ms =  Math.Min(GameManager.m_local_player.m_avg_ping_ms,
                                      Menus.mms_weapon_lag_compensation_max);
            return (Menus.mms_weapon_lag_compensation_scale / 100f) * time_ms / 1000f;

        }

        // How far ahead to advance ships, in seconds.
        public static float GetShipExtrapolationTime() {
            if (MPObserver.Enabled) {
                return 0f;
            }

            float time_ms =  Math.Min(GameManager.m_local_player.m_avg_ping_ms,
                                      Menus.mms_ship_lag_compensation_max);
            time_ms = (Menus.mms_ship_lag_compensation_scale / 100f) * time_ms / 1000f;
            return time_ms + (Menus.mms_ship_lag_compensation_offset/1000.0f);
        }

        public static void InitForMatch() {
            bodies_to_resolve.Clear();
            //players_to_resolve.Clear();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MPClientExtrapolation_InitBeforeEachMatch {
        private static void Postfix() {
            MPClientExtrapolation.InitForMatch();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateAll")]
    class MPClientExtrapolation_FixedUpdateAll {
        private static void Postfix() {
            if (MPClientExtrapolation.bodies_to_resolve.Count == 0/* && MPClientExtrapolation.players_to_resolve.Count == 0 */) {
                return;
            }

            var amount = MPClientExtrapolation.GetWeaponExtrapolationTime();
            if (amount <= 0f) {
                return;
            }

            //foreach (var kvp in MPClientExtrapolation.players_to_resolve) {
            //    var player = kvp.Key;
            //    var velocities = kvp.Value;
            //    player.c_player_ship.c_transform.localPosition = velocities.LocalPosition;
            //    player.c_player_ship.c_transform.rotation = velocities.Rotation;
            //    player.c_player_ship.c_mesh_collider_trans.localPosition = player.c_player_ship.c_transform.localPosition;
            //}

            NetworkSim.PauseAllRigidBodiesExcept(null);

            foreach (var body in MPClientExtrapolation.bodies_to_resolve) {
                if (NetworkSim.m_paused_rigid_bodies.ContainsKey(body)) {
                    var state = NetworkSim.m_paused_rigid_bodies[body];
                    body.isKinematic = false;
                    body.velocity = state.m_velocity;
                    body.angularVelocity = state.m_angular_velocity;
                }
            }

            //foreach (var kvp in MPClientExtrapolation.players_to_resolve) {
            //    var player = kvp.Key;
            //    var velocities = kvp.Value;
            //    player.c_player_ship.c_rigidbody.isKinematic = false;
            //    player.c_player_ship.c_rigidbody.velocity = velocities.Velocity;
            //    player.c_player_ship.c_rigidbody.angularVelocity = velocities.AngularVelocity;
            //    player.c_player_ship.c_rigidbody.drag = 0f;
            //}

            Physics.Simulate(amount);

            //foreach (var kvp in MPClientExtrapolation.players_to_resolve) {
            //    var player = kvp.Key;
            //    var velocities = kvp.Value;
            //    player.c_player_ship.c_rigidbody.isKinematic = false;
            //    player.c_player_ship.c_rigidbody.velocity = Vector3.zero;
            //    player.c_player_ship.c_rigidbody.angularVelocity = Vector3.zero;
            //    player.c_player_ship.c_rigidbody.drag = velocities.Drag;
            //}

            NetworkSim.ResumeAllPausedRigidBodies();

            MPClientExtrapolation.bodies_to_resolve.Clear();
            //MPClientExtrapolation.players_to_resolve.Clear();
        }
    }

    [HarmonyPatch(typeof(ProjectileManager), "FireProjectile")]
    class MPClientExtrapolation_FireProjectile {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                // Replace where the projectile is added to the list of projectiles with our own function.
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "Fire") {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPClientExtrapolation), "LerpProjectile"));
                    Debug.Log("Patched FireProjectile for MPClientExtrapolation");
                    continue;
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(Client), "UpdateInterpolationBuffer")]
    class MPClientExtrapolation_UpdateInterpolationBuffer {

        static bool Prefix(){
            MPPlayerStateDump.buf.AddUpdateBegin(Time.time,Client.m_InterpolationStartTime);
            if (Client.m_InterpolationBuffer[0] == null)
            {
                while (Client.m_PendingPlayerSnapshotMessages.Count > 3)
                {
                    Client.m_PendingPlayerSnapshotMessages.Dequeue();
                }
                if (Client.m_PendingPlayerSnapshotMessages.Count >= 3) {
                    Client.m_InterpolationBuffer[0] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationBuffer[1] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationBuffer[2] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationStartTime = Time.time;
                }
                MPPlayerStateDump.buf.AddUpdateEnd(Client.m_InterpolationStartTime);
                return false;
            }
            else
            {
                if(Client.m_PendingPlayerSnapshotMessages.Count < 1){
                    MPPlayerStateDump.buf.AddUpdateEnd(Client.m_InterpolationStartTime);
                    return false;
                }
                else if(Client.m_PendingPlayerSnapshotMessages.Count == 1){
                    Client.m_InterpolationBuffer[0] = Client.m_InterpolationBuffer[1];
                    Client.m_InterpolationBuffer[1] = Client.m_InterpolationBuffer[2];
                    Client.m_InterpolationBuffer[2] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationStartTime += Time.fixedDeltaTime;
                }
                else if (Client.m_PendingPlayerSnapshotMessages.Count == 2) {
                    Client.m_InterpolationBuffer[0] = Client.m_InterpolationBuffer[2];
                    Client.m_InterpolationBuffer[1] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationBuffer[2] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationStartTime += 2.0f *Time.fixedDeltaTime;
                } else {
                    while (Client.m_PendingPlayerSnapshotMessages.Count > 3)
                    {
                        Client.m_PendingPlayerSnapshotMessages.Dequeue();
                        Client.m_InterpolationStartTime += Time.fixedDeltaTime;
                    }
                    Client.m_InterpolationBuffer[0] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationBuffer[1] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationBuffer[2] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationStartTime += 3.0f *Time.fixedDeltaTime;
                }
                while (Client.m_InterpolationStartTime + 1.9f * Time.fixedDeltaTime < Time.time) {
                    Client.m_InterpolationStartTime += Time.fixedDeltaTime;
                }
                if (Client.m_InterpolationStartTime > Time.time) {
                    Client.m_InterpolationStartTime = Time.time;
                }

                MPPlayerStateDump.buf.AddUpdateEnd(Client.m_InterpolationStartTime);
                return false;
            }
        }
    }

    

    [HarmonyPatch(typeof(Client), "InterpolateRemotePlayers")]
    class MPClientExtrapolation_InterpolateRemotePlayers {
        static private MethodInfo _Client_GetPlayerSnapshotFromInterpolationBuffer_Method = AccessTools.Method(typeof(Client), "GetPlayerSnapshotFromInterpolationBuffer");

        static bool Prefix() {
            int ping = GameManager.m_local_player.m_avg_ping_ms;
			MPPlayerStateDump.buf.AddInterpolateBegin(Time.time, ping);
            if (Client.m_InterpolationBuffer[0] == null || Client.m_InterpolationBuffer[1] == null || Client.m_InterpolationBuffer[2] == null) {
				MPPlayerStateDump.buf.AddCommand((uint)MPPlayerStateDump.Command.INTERPOLATE_END);
                return true;
            }
            float num = CalculateLerpParameter();
            PlayerSnapshotToClientMessage msg0;
            PlayerSnapshotToClientMessage msg1;
            PlayerSnapshotToClientMessage msg2;
            msg0 = Client.m_InterpolationBuffer[0];
            msg1 = Client.m_InterpolationBuffer[1];
            msg2 = Client.m_InterpolationBuffer[2];
            foreach (Player player in Overload.NetworkManager.m_Players) {
                if (player != null && !player.isLocalPlayer && !player.m_spectator) {
                    PlayerSnapshot A,B;
                    if (num < 1.0f) {
                        A = (PlayerSnapshot)_Client_GetPlayerSnapshotFromInterpolationBuffer_Method.Invoke(null, new object[] { player, msg0 });
                        B = (PlayerSnapshot)_Client_GetPlayerSnapshotFromInterpolationBuffer_Method.Invoke(null, new object[] { player, msg1 });
                    } else {
                        A = (PlayerSnapshot)_Client_GetPlayerSnapshotFromInterpolationBuffer_Method.Invoke(null, new object[] { player, msg1 });
                        B = (PlayerSnapshot)_Client_GetPlayerSnapshotFromInterpolationBuffer_Method.Invoke(null, new object[] { player, msg2 });
                        num -= 1.0f;
                    }
		    num=1;
                    A = (PlayerSnapshot)_Client_GetPlayerSnapshotFromInterpolationBuffer_Method.Invoke(null, new object[] { player, msg2 });

                    if (A != null /* && B != null */) {
                        LerpRemotePlayer(player, A,A, num);
                    }
                }
            }
			MPPlayerStateDump.buf.AddCommand((uint)MPPlayerStateDump.Command.INTERPOLATE_END);
            return false;
        }

        static void LerpRemotePlayer(Player player, PlayerSnapshot A, PlayerSnapshot B, float t) {
			MPPlayerStateDump.buf.AddLerpBegin(player.m_lerp_wait_for_respawn_pos,ref A,ref B,t);
            if (player.m_lerp_wait_for_respawn_pos) {
                player.LerpRemotePlayer(A, B, t);
				MPPlayerStateDump.buf.AddLerpEnd(player.m_lerp_wait_for_respawn_pos);
                return;
            }

            // Lookahead in frames.
            float lookahead = (MPClientExtrapolation.GetShipExtrapolationTime() / Time.fixedDeltaTime);

            // reduce oversteer by extrapolating less for rotation
            var rot_lookahead = lookahead * .5f;
	    lookahead = 0.0f;
	    rot_lookahead = 0.0f;

            player.c_player_ship.c_transform.localPosition = Vector3.LerpUnclamped(A.m_pos, B.m_pos, t + lookahead);
            player.c_player_ship.c_transform.rotation = Quaternion.SlerpUnclamped(A.m_rot, B.m_rot, t + rot_lookahead);
            player.c_player_ship.c_mesh_collider_trans.localPosition = player.c_player_ship.c_transform.localPosition;

            //// Bail if we're observing.
            //var factor = MPClientExtrapolation.GetFactor();
            //if (factor == 0f || GameManager.m_local_player.m_avg_ping_ms <= 0f) {
            //    // Reposition ship 1 frame ahead to compensate for showing old position data.
            //    __instance.c_player_ship.c_transform.localPosition = Vector3.LerpUnclamped(A.m_pos, B.m_pos, t + 1);
            //    __instance.c_player_ship.c_transform.rotation = Quaternion.SlerpUnclamped(A.m_rot, B.m_rot, t + 1);
            //    __instance.c_player_ship.c_mesh_collider_trans.localPosition = __instance.c_player_ship.c_transform.localPosition;
            //    return false;
            //}

            //// Queue to simulate physics.
            //B.m_rot.ToAngleAxis(out var B_angle, out var B_axis);
            //A.m_rot.ToAngleAxis(out var A_angle, out var A_axis);
            //MPClientExtrapolation.players_to_resolve[__instance] = new MPClientExtrapolation_Velocities {
            //    Velocity = __instance.c_player_ship.c_rigidbody.velocity = (B.m_pos - A.m_pos) / Time.fixedDeltaTime,
            //    AngularVelocity = ((B_angle * B_axis * Mathf.Deg2Rad) / Time.fixedDeltaTime) - ((A_angle * A_axis * Mathf.Deg2Rad) / Time.fixedDeltaTime),
            //    Drag = __instance.c_player_ship.c_rigidbody.drag,
            //    LocalPosition = Vector3.LerpUnclamped(A.m_pos, B.m_pos, t + 1),
            //    Rotation = Quaternion.SlerpUnclamped(A.m_rot, B.m_rot, t + 1)
            //};
			MPPlayerStateDump.buf.AddLerpEnd(player.m_lerp_wait_for_respawn_pos);
        }

        // Not the same as vanilla
        private static float CalculateLerpParameter() {
            float num = Mathf.Max(0f, Time.time - Client.m_InterpolationStartTime);
            return num / Time.fixedDeltaTime;
        }
    }
}
