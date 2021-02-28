using System;
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
            float time_ms = Math.Min(GameManager.m_local_player.m_avg_ping_ms,
                                      Menus.mms_weapon_lag_compensation_max);
            return (Menus.mms_weapon_lag_compensation_scale / 100f) * time_ms / 1000f;

        }

        // How far ahead to advance ships, in seconds.
        public static float GetShipExtrapolationTime() {
            if (MPObserver.Enabled) {
                return 0f;
            }
            float time_ms = Math.Min(GameManager.m_local_player.m_avg_ping_ms,
                                      Menus.mms_ship_lag_compensation_max);
            return (Menus.mms_ship_lag_compensation_scale / 100f) * time_ms / 1000f;

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

    class MPClientShipReckoning{
        // ship interpolation OR extrapolation for clients
        public static float m_last_update_time;
        public static float m_last_frame_time;

        // simple statistic
        public static float m_compensation_sum;
        public static int m_compensation_count;
        public static int m_compensation_interpol_count;
        public static float m_compensation_last;

        // simple ring buffer, use size 4 which is a power of two, so the % 4 becomes simple & 3
        private static NewPlayerSnapshotToClientMessage[] m_last_messages_ring = new NewPlayerSnapshotToClientMessage[4];
        private static int m_last_messages_ring_count = 0;
        private static int m_last_messages_ring_pos_last = 3; // position of the last added element
        private static object m_last_messages_lock = new object();
        private static float m_last_message_timestamp = -1.0f;

        private static void EnqueueToRing(NewPlayerSnapshotToClientMessage msg)
        {
            m_last_messages_ring_pos_last = (m_last_messages_ring_pos_last + 1) & 3;
            m_last_messages_ring[m_last_messages_ring_pos_last] = msg;
            if (m_last_messages_ring_count < 4) {
                m_last_messages_ring_count++;
            }
            m_last_message_timestamp = msg.m_timestamp;
            //Debug.LogFormat("Adding {0} at {1}, have {2}", msg.m_timestamp, Time.time, m_last_messages_ring_count);
        }

        private static void ClearRing()
        {
            m_last_messages_ring_pos_last = 3;
            m_last_messages_ring_count = 0;
            m_last_message_timestamp = -1.0f;
        }

        public static void ResetForNewMatch()
        {
            lock (m_last_messages_lock) {
                ClearRing();
                m_last_update_time = Time.time;
                m_last_frame_time = Time.time;
            }
            m_compensation_sum = 0.0f;
            m_compensation_count = 0;
            m_compensation_interpol_count = 0;
            m_compensation_last = Time.time;
        }

        // interpolate a single NewPlayerSnapshot (including the extra fields besides pos and rot)!
        private static void InterpolatePlayerSnapshot(ref NewPlayerSnapshot C, NewPlayerSnapshot A, NewPlayerSnapshot B, float t)
        {
            C.m_pos = Vector3.LerpUnclamped(A.m_pos, B.m_pos, t);
            C.m_rot = Quaternion.SlerpUnclamped(A.m_rot, B.m_rot, t);
            C.m_vel = Vector3.LerpUnclamped(A.m_vel, B.m_vel, t);
            Quaternion A_vrot = Quaternion.Euler(A.m_vrot);
            Quaternion B_vrot = Quaternion.Euler(C.m_vrot);
            Quaternion C_vrot = Quaternion.SlerpUnclamped(A_vrot,B_vrot, t);
            C.m_vrot = C_vrot.eulerAngles;
            C.m_net_id = A.m_net_id;
        }

        // extrapolate a single NewPlayerSnapshot (including the extra fields besides pos and rot)!
        private static void ExtrapolatePlayerSnapshot(ref NewPlayerSnapshot C, NewPlayerSnapshot B, float t)
        {
            C.m_pos = Vector3.LerpUnclamped(B.m_pos, B.m_pos + B.m_vel, t);
            C.m_rot = Quaternion.SlerpUnclamped(B.m_rot, B.m_rot * Quaternion.Euler(B.m_vrot), t);
            // assume the rest stays the same
            C.m_vel = B.m_vel;
            C.m_vrot = B.m_vrot;
            C.m_net_id = B.m_net_id;
        }

        // interpolate a whole NewPlayerSnapshotToClientMessage
        // interpolate between A and B, t is the relative factor between both
        // If a player is not in both messages, it will not be in the resulting messages
        private static NewPlayerSnapshotToClientMessage InterpolatePlayerSnapshotMessage(NewPlayerSnapshotToClientMessage A, NewPlayerSnapshotToClientMessage B, float t)
        {
            NewPlayerSnapshotToClientMessage C = new NewPlayerSnapshotToClientMessage();
            int i,j;

            C.m_num_snapshots = 0;

            for (i=0; i<A.m_num_snapshots; i++) {
                for (j=0; j<B.m_num_snapshots; j++) {
                    if (A.m_snapshots[i].m_net_id.Value == B.m_snapshots[j].m_net_id.Value) {
                        InterpolatePlayerSnapshot(ref C.m_snapshots[C.m_num_snapshots++], A.m_snapshots[i], B.m_snapshots[j], t);
                    }
                }
            }
            
            C.m_timestamp = (1.0f - t) * A.m_timestamp + t*B.m_timestamp;
            return C;
        }

        // extrapolate a whole NewPlayerSnapshotToClientMessage
        // extrapolate from B into t seconds into the future, t can be negative
        private static NewPlayerSnapshotToClientMessage ExtrapolatePlayerSnapshotMessage(NewPlayerSnapshotToClientMessage B, float t)
        {
            NewPlayerSnapshotToClientMessage C = new NewPlayerSnapshotToClientMessage();
            int i;

            for (i=0; i<B.m_num_snapshots; i++) {
                ExtrapolatePlayerSnapshot(ref C.m_snapshots[C.m_num_snapshots++], B.m_snapshots[i], t);
            }

            C.m_timestamp += t;
            return C;
        }

        public static void AddNewPlayerSnapshot(NewPlayerSnapshotToClientMessage msg)
        {
            lock (m_last_messages_lock) {
                if  (m_last_message_timestamp < 0.0f) {
                    // first packet
                    EnqueueToRing(msg);
                    m_last_update_time = Time.time; 
                } else {
                    // ARGH, currently, the time base we get from the server is broken,
                    //int deltaFrames = (int)((msg.m_timestamp - m_last_message_timestamp) / Time.fixedDeltaTime + 0.5f);
                    int deltaFrames = 1; // don't use it for now

                    if (deltaFrames == 1) {
                        // next in sequence, as we expected
                        EnqueueToRing(msg);
                        m_last_update_time += Time.fixedDeltaTime; 
                    } else if (deltaFrames > 1) {
                        Debug.LogFormat("detected {0} missing snapshots",  deltaFrames - 1);
                        if (Menus.mms_ship_max_interpolate_frames > 0) {
                            // this is the slow path, only used if interpolation is enabled
                            // we actually do the creation of missing packets here
                            // one per received new snapshot, so that the per-frame
                            // update code path can stay simple 
                            NewPlayerSnapshotToClientMessage lastMsg = m_last_messages_ring[m_last_messages_ring_pos_last];
                            if (deltaFrames == 2) {
                                // there is one missing snapshot
                                EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.5f));
                                EnqueueToRing(msg);
                                m_last_update_time += 2.0f * Time.fixedDeltaTime; 
                            } else if (deltaFrames == 3) {
                                // there are two missing snapshots
                                EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.3333f));
                                EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.6666f));
                                EnqueueToRing(msg);
                                m_last_update_time += 3.0f * Time.fixedDeltaTime; 
                            } else if (deltaFrames ==  3) {
                                // there are three missing snapshots
                                EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.25f));
                                EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.5f));
                                EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.75f));
                                EnqueueToRing(msg);
                                m_last_update_time += 3.0f * Time.fixedDeltaTime; 
                            } else {
                                // there are more than 3 missing snapshots,
                                // just take the completely new data in
                                EnqueueToRing(ExtrapolatePlayerSnapshotMessage(msg, -3.0f * Time.fixedDeltaTime));
                                EnqueueToRing(ExtrapolatePlayerSnapshotMessage(msg, -2.0f * Time.fixedDeltaTime));
                                EnqueueToRing(ExtrapolatePlayerSnapshotMessage(msg, -1.0f * Time.fixedDeltaTime));
                                EnqueueToRing(msg);
                                m_last_update_time += deltaFrames * Time.fixedDeltaTime; 
                            }
                        } else {
                            // If interpolation is disabled, the per-frame update
                            // will only ever access the latest element in the ring buffer anyway
                            // so there is no point in trying to approximate previous states
                            EnqueueToRing(msg);
                            m_last_update_time += deltaFrames * Time.fixedDeltaTime; 
                        }
                    } else { 
                        // duplicate? probably never happens, but we're going to ignore it
                       if (deltaFrames < -10) {
                           Debug.Log("re-syncing to server packets");
                           // we are out of sync with the server
                           ResetForNewMatch();
                           EnqueueToRing(msg);
                           m_last_update_time = Time.time; 
                       } else {
                           Debug.LogFormat("got snapshot {0} frames from the past?! ignoring it!", -deltaFrames);
                           NewPlayerSnapshotToClientMessage lastMsg = m_last_messages_ring[m_last_messages_ring_pos_last];
                           Debug.LogFormat("last is {0} {1}, new is {2}, move {3} {4}",
                                           m_last_message_timestamp, lastMsg.m_timestamp,
                                           msg.m_timestamp,
                                           lastMsg.m_snapshots[0].m_pos.x,
                                           msg.m_snapshots[0].m_pos.x);
                       }
                       return;
                    }
                }
                // check if the time base is still plausible
                float delta = (Time.time - m_last_update_time)/ Time.fixedDeltaTime; // in ticks
                // allow a sliding window to catch up for latency jitter
                float frameSoftSyncLimit = 2.0f; ///hard-sync if we're off by more than that many physics ticks
                if (delta < -frameSoftSyncLimit || delta > frameSoftSyncLimit) {
                    // hard resync
                    Debug.LogFormat("hard resync by {0} frames", delta);
                    m_last_update_time = Time.time;
                } else {
                    // soft resync
                    m_last_update_time += 0.1f * delta * Time.fixedDeltaTime;
                }
            } // end lock 
        }

        public static NewPlayerSnapshotToClientMessage m_last_update = new NewPlayerSnapshotToClientMessage();

        static private MethodInfo _Client_GetPlayerFromNetId_Method = AccessTools.Method(typeof(Client), "GetPlayerFromNetId");

        public static NewPlayerSnapshot GetPlayerSnapshot(NewPlayerSnapshotToClientMessage msg, Player p)
        {
            for (int i = 0; i < msg.m_num_snapshots; i++)
            {
                NewPlayerSnapshot playerSnapshot = msg.m_snapshots[i];
                Player candidate = (Player)_Client_GetPlayerFromNetId_Method.Invoke(null, new object[] {playerSnapshot.m_net_id});

                if (candidate == p)
                {
                    return playerSnapshot;
                }
            }
            return null;
        }

        public static void interpolatePlayer(Player player, NewPlayerSnapshot A, NewPlayerSnapshot B, float t)
        {
            player.c_player_ship.c_transform.localPosition = Vector3.LerpUnclamped(A.m_pos, B.m_pos, t);
            player.c_player_ship.c_transform.rotation = Quaternion.SlerpUnclamped(A.m_rot, B.m_rot, t);
            player.c_player_ship.c_mesh_collider_trans.localPosition = player.c_player_ship.c_transform.localPosition;
        }

        public static void extrapolatePlayer(Player player, NewPlayerSnapshot snapshot, float t){
            player.c_player_ship.c_transform.localPosition = Vector3.LerpUnclamped(snapshot.m_pos, snapshot.m_pos+snapshot.m_vel, t);
            player.c_player_ship.c_transform.rotation = Quaternion.SlerpUnclamped(snapshot.m_rot, snapshot.m_rot*Quaternion.Euler(snapshot.m_vrot), t);
            player.c_player_ship.c_mesh_collider_trans.localPosition = player.c_player_ship.c_transform.localPosition;
        }

        public static void updatePlayerPositions()
        {
            // float now = NetworkMatch.m_match_elapsed_seconds; // wholycow uses a different time base
            float now = Time.time;
            NewPlayerSnapshotToClientMessage msgA = null; // interpolation: start 
            NewPlayerSnapshotToClientMessage msgB = null; // interpolation: end, extrapolation start:
            float interpolate_factor = 0.0f;              // interpolation: factor in [0,1]
            float delta_t = 0.0f;
            int interpolate_frames = 0;

            // find out which case we have, and get the relevant snapshot message(s)
            lock (m_last_messages_lock) {
                /*
                for (int xxx=0; xxx<m_last_messages_ring_count; xxx++) {
                    Debug.LogFormat("having snapshot from {0} represents {1}", m_last_messages_ring[(m_last_messages_ring_pos_last + 4 - xxx)&3].m_timestamp, m_last_update_time - xxx* Time.fixedDeltaTime);
                }
                */

                delta_t = now + MPClientExtrapolation.GetShipExtrapolationTime() - m_last_update_time;
                // if we want interpolation, add this as negative offset
                // we use 0 as the base point which exacty matches the state of the last received message
                // (as whollycow's code also did)
                delta_t -=  Menus.mms_ship_max_interpolate_frames * Time.fixedDeltaTime;
                // time difference in physics frames 
                float delta_frames = delta_t / Time.fixedDeltaTime;
                interpolate_frames = -(int)Mathf.Floor(delta_frames);

                if (interpolate_frames > 0) {
                    // we need interpolate_frames + 1 frames in the buffer
                    if ( interpolate_frames >= m_last_messages_ring_count ) {
                        // not enough packets received so far
                        // TODO: this is wrong: we could to extrapolation into the past
                        return;
                    }
                    msgA = m_last_messages_ring[(m_last_messages_ring_pos_last + 4 - interpolate_frames) & 3];
                    msgB = m_last_messages_ring[(m_last_messages_ring_pos_last + 5 - interpolate_frames) & 3];
                    interpolate_factor = delta_frames - Mathf.Floor(delta_frames);
                } else {
                    // extrapolation case
                    // use the most recently received snapshot
                    msgB = m_last_messages_ring[m_last_messages_ring_pos_last];
                }
            } // lock
            m_last_frame_time = now;

            /*
            Debug.LogFormat("At: {0} Setting: {1} IntFrames: {2}, dt: {3}, IntFact {4}",now,Menus.mms_ship_max_interpolate_frames, interpolate_frames, delta_t, interpolate_factor);
            if (interpolate_frames > 0) {
                Debug.LogFormat("Using A from {0}", msgA.m_timestamp);
                Debug.LogFormat("Using B from {0}", msgB.m_timestamp);
            } else {
                Debug.LogFormat("Using B from {0}", msgB.m_timestamp);
            }
            */

            // keep statistics
            m_compensation_sum += delta_t;
            m_compensation_count++;
            m_compensation_interpol_count += (interpolate_frames > 0)?1:0;
            if (Time.time >= m_compensation_last + 5.0 && m_compensation_count > 0) {
                Debug.LogFormat("ship lag compensation over last {0} frames: {1}ms / {2} physics ticks, {3} interpolation ({4}%)",
                                m_compensation_count, 1000.0f* (m_compensation_sum/ m_compensation_count),
                                (m_compensation_sum/m_compensation_count)/Time.fixedDeltaTime,
                                m_compensation_interpol_count,
                                100.0f*((float)m_compensation_interpol_count/(float)m_compensation_count));
                m_compensation_sum = 0.0f;
                m_compensation_count = 0;
                m_compensation_interpol_count = 0;
                m_compensation_last = Time.time;
            }

            // actually apply the operation to each player
            foreach (Player player in Overload.NetworkManager.m_Players)
            {
                if (player != null && !player.isLocalPlayer && !player.m_spectator)
                {
                    if (interpolate_frames > 0) {
                        NewPlayerSnapshot A = GetPlayerSnapshot(msgA, player);
                        NewPlayerSnapshot B = GetPlayerSnapshot(msgB, player);
                        if(A != null && B != null){
                            interpolatePlayer(player, A, B, interpolate_factor);
                        }
                    } else {
                        NewPlayerSnapshot snapshot = GetPlayerSnapshot(msgB, player);
                        if(snapshot != null){
                            extrapolatePlayer(player, snapshot, delta_t);
                        }
                    }
                }
            }
        }
    }

    // called per frame
    [HarmonyPatch(typeof(Client), "InterpolateRemotePlayers")]
    class MPClientExtrapolation_ClientUpdate{
        static bool Prefix(){
            // This function is called once per frame from Client.Update()
            if (Overload.NetworkManager.IsServer() || NetworkMatch.m_match_state != MatchState.PLAYING) {
                return true;
            }
            MPClientShipReckoning.updatePlayerPositions();
            return false;
        }

    }

    // called per physics update
    [HarmonyPatch(typeof(Client), "FixedUpdate")]
    class MPClientExtrapolation_ClientFixedUpdate{
        static bool Prefix(){
            if (Overload.NetworkManager.IsServer() || NetworkMatch.m_match_state != MatchState.PLAYING) {
                return true;
            }
            // Client.FixedUpdate() did nothing except call UpdateInterpolationBuffer,
            // which we now ignore
            return false;
        }
    }

    // called when connectig to the server
    [HarmonyPatch(typeof(Client), "Connect")]
    class MPPlayerStateDump_Connect {
        private static void Postfix()
        {
            if (Overload.NetworkManager.IsServer()) {
                return;
            }
            MPClientShipReckoning.ResetForNewMatch();
        }
    }

    /*[HarmonyPatch(typeof(Client), "UpdateInterpolationBuffer")]
    class MPClientExtrapolation_UpdateInterpolationBuffer {

        static bool Prefix(){
            if (Client.m_InterpolationBuffer[0] == null)
            {
                return true;
            }
            else
            {
                if(Client.m_PendingPlayerSnapshotMessages.Count < 1){
                    return false;
                }
                else if(Client.m_PendingPlayerSnapshotMessages.Count == 1){
                    Client.m_InterpolationBuffer[1] = Client.m_InterpolationBuffer[2];
                    Client.m_InterpolationBuffer[2] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                }
                else{
                    while (Client.m_PendingPlayerSnapshotMessages.Count > 2)
                    {
                        Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    }
                    Client.m_InterpolationBuffer[1] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                    Client.m_InterpolationBuffer[2] = Client.m_PendingPlayerSnapshotMessages.Dequeue();
                }
                Client.m_InterpolationStartTime = Time.time;

                return false;
            }
        }
    }*/

    /*[HarmonyPatch(typeof(Client), "InterpolateRemotePlayers")]
    class MPClientExtrapolation_InterpolateRemotePlayers {
        static private MethodInfo _Client_GetPlayerSnapshotFromInterpolationBuffer_Method = AccessTools.Method(typeof(Client), "GetPlayerSnapshotFromInterpolationBuffer");

        static bool Prefix() {
            if (Client.m_InterpolationBuffer[0] == null || Client.m_InterpolationBuffer[1] == null || Client.m_InterpolationBuffer[2] == null) {
                return true;
            }
            float num = CalculateLerpParameter();
            PlayerSnapshotToClientMessage msg;
            PlayerSnapshotToClientMessage msg2;
            msg = Client.m_InterpolationBuffer[1];
            msg2 = Client.m_InterpolationBuffer[2];
            foreach (Player player in Overload.NetworkManager.m_Players) {
                if (player != null && !player.isLocalPlayer && !player.m_spectator) {
                    PlayerSnapshot playerSnapshotFromInterpolationBuffer = (PlayerSnapshot)_Client_GetPlayerSnapshotFromInterpolationBuffer_Method.Invoke(null, new object[] { player, msg });
                    PlayerSnapshot playerSnapshotFromInterpolationBuffer2 = (PlayerSnapshot)_Client_GetPlayerSnapshotFromInterpolationBuffer_Method.Invoke(null, new object[] { player, msg2 });
                    if (playerSnapshotFromInterpolationBuffer != null && playerSnapshotFromInterpolationBuffer2 != null) {
                        LerpRemotePlayer(player, playerSnapshotFromInterpolationBuffer, playerSnapshotFromInterpolationBuffer2, num);
                    }
                }
            }
            return false;
        }

        static void LerpRemotePlayer(Player player, PlayerSnapshot A, PlayerSnapshot B, float t) {
            if (player.m_lerp_wait_for_respawn_pos) {
                player.LerpRemotePlayer(A, B, t);
                return;
            }

            // Lookahead in frames.
            float lookahead = MPClientExtrapolation.GetShipExtrapolationTime() / Time.fixedDeltaTime;

            // reduce oversteer by extrapolating less for rotation
            var rot_lookahead = lookahead * .5f;

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
        }

        // Not the same as vanilla
        private static float CalculateLerpParameter() {
            float num = Mathf.Max(0f, Time.time - Client.m_InterpolationStartTime);
            return num / Time.fixedDeltaTime;
        }
    }*/
}
