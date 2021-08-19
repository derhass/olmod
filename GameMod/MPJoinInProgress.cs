﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod {
    class MPJoinInProgress
    {
        public const int NET_VERSION_JIP = 1;

        public static bool NetworkMatchEnabled = true;
        public static bool MenuManagerEnabled = true;
        public static bool SingleMatchEnable = false;

        public static List<NetworkInstanceId> PlayersReady = new List<NetworkInstanceId>();
        public static Queue<int> ClientsInJoinPhase = new Queue<int>();

        public static bool MatchHasStartedMod(bool m_match_has_started)
        {
            return m_match_has_started && (!NetworkMatchEnabled || (int)NetworkMatch.GetMatchState() >= (int)MatchState.POSTGAME);
        }

        private static FieldInfo _NetworkMatch_m_match_force_playlist_level_idx = typeof(NetworkMatch).GetField("m_match_force_playlist_level_idx", AccessTools.all);
        public static string NetworkMatchLevelName()
        {
            int m_match_force_playlist_level_idx = (int)_NetworkMatch_m_match_force_playlist_level_idx.GetValue(null);
            if (m_match_force_playlist_level_idx < 0 || m_match_force_playlist_level_idx >= GameManager.MultiplayerMission.NumLevels)
                return null;
            if (GameManager.MultiplayerMission.IsLevelAnAddon(m_match_force_playlist_level_idx))
                return GameManager.MultiplayerMission.GetAddOnLevelIdStringHash(m_match_force_playlist_level_idx);
            return GameManager.MultiplayerMission.GetLevelFileName(m_match_force_playlist_level_idx);
        }

        public static void SetReady(Player player, bool ready)
        {
            UnityEngine.Debug.LogFormat("XXXX READY: {0} = {1}", player.netId, ready);
            // Keep track of who has been set ready so we're not wasting CPU cycles making someone visible who is already visible.  Especially needed with the JIP visibility workaround.
            if (ready)
            {
                if (PlayersReady.Contains(player.netId))
                {
                    return;
                }
                PlayersReady.Add(player.netId);
            }
            else
            {
                PlayersReady.Remove(player.netId);
            }

            player.c_player_ship.c_cockpit.gameObject.SetActive(ready);
            player.c_player_ship.c_mesh_collider.enabled = ready;
            player.c_player_ship.c_mesh_collider.gameObject.SetActive(ready);
            player.c_player_ship.c_level_collider.enabled = ready;
            player.c_player_ship.c_level_collider.gameObject.SetActive(ready);
            player.c_player_ship.gameObject.layer = ready ? 9 : 2;
            player.c_player_ship.enabled = ready;
            player.c_player_ship.gameObject.SetActive(ready);
            player.enabled = ready;
            player.gameObject.SetActive(ready);

            if (ready)
            {
                player.c_player_ship.RestoreLights();
            }
            else
            {
                player.c_player_ship.DeactivateLights();
            }
        }
    }

    public class JIPJustJoinedMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)MPJoinInProgress.NET_VERSION_JIP);
            writer.Write(playerId);
            writer.Write(ready);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            playerId = reader.ReadNetworkId();
            ready = reader.ReadBoolean();
        }

        public NetworkInstanceId playerId;
        public bool ready;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPJoinInProgressClientHandlers
    {
        private static void OnJIPJustJoinedMessage(NetworkMessage rawMsg)
        {
            if (Server.IsActive())
            {
                return;
            }

            var msg = rawMsg.ReadMessage<JIPJustJoinedMessage>();

            UnityEngine.Debug.LogFormat("XXXX RECV MESG: {0} {1}", msg.playerId, msg.ready);

            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.playerId);

            if (!player)
            {
                return;
            }

            MPJoinInProgress.SetReady(player, msg.ready);
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(MessageTypes.MsgJIPJustJoined, OnJIPJustJoinedMessage);
        }
    }

    class Trans
    {
        public static IEnumerable<CodeInstruction> FieldReadModifier(string fieldName, MethodInfo method, IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                yield return code;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == fieldName)
                    yield return new CodeInstruction(OpCodes.Call, method);
            }
        }

        public static IEnumerable<CodeInstruction> ReplaceCall(string origMethodName, MethodInfo newMethodInfo, IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == origMethodName)
                    code.operand = newMethodInfo;
                yield return code;
            }
        }
    }

    [HarmonyPatch]
    class JIPStartBackfill
    {
        private static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all).GetMethod("StartMatchmakerBackfill");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch]
    class JIPDoTick
    {
        private static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all).GetMethod("DoTick");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch]
    class JIPOnUpdateGS
    {
        private static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all).GetMethod("OnUpdateGameSession");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ProcessLobby")]
    class JIPProcessLobby
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "NetSystemOnUpdateGameSession")]
    class JIPNetOnUpdateGS
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "NetSystemNotifyPlayerConnected")]
    class JIPPlayerConn
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "AcceptNewConnection")]
    class JIPAcceptConn
    {
        public static bool MaybeInLobby()
        {
            return MPJoinInProgress.NetworkMatchEnabled || NetworkMatch.InLobby();
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.ReplaceCall("InLobby", AccessTools.Method(typeof(JIPAcceptConn), "MaybeInLobby"), codes);
        }
    }

    [HarmonyPatch(typeof(Server), "OnPlayerJoinLobbyMessage")]
    class JIPJoinLobby
    {
        private static IEnumerator SendSceneLoad(int connectionId)
        {
            // wait until we've received the loadout
            while (!NetworkMatch.m_player_loadout_data.ContainsKey(connectionId))
            {
                if (!NetworkMatch.m_players.ContainsKey(connectionId)) // disconnected?
                    yield break;
                yield return null;
            }

            StringMessage levelNameMsg = new StringMessage(MPJoinInProgress.NetworkMatchLevelName());
            NetworkServer.SendToClient(connectionId, CustomMsgType.SceneLoad, levelNameMsg);
            Debug.Log("JIP: sending scene load " + levelNameMsg.value);

            if (NetworkMatch.GetMatchState() == MatchState.LOBBY_LOADING_SCENE)
                yield break;

            StringMessage sceneNameMsg = new StringMessage(GameplayManager.m_level_info.SceneName);
            NetworkServer.SendToClient(connectionId, CustomMsgType.SceneLoaded, sceneNameMsg);
            Debug.Log("JIP: sending scene loaded " + sceneNameMsg.value);
        }

        private static void Postfix(NetworkMessage msg)
        {
            MatchState match_state = NetworkMatch.GetMatchState();
            if (match_state != MatchState.LOBBY && match_state != MatchState.LOBBY_LOAD_COUNTDOWN)
            {
                GameManager.m_gm.StartCoroutine(SendSceneLoad(msg.conn.connectionId));
            }
        }
    }

    [HarmonyPatch(typeof(Server), "OnReadyForCountdownToServer")]
    class JIPReadyForCountdown
    {
        private static void SendMatchState(int connectionId)
        {
            if (NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
                foreach (var team in MPTeams.ActiveTeams)
                    NetworkServer.SendToClient(connectionId, CustomMsgType.SetScoreForTeam, new ScoreForTeamMessage
                    {
                        team = (int)team,
                        score = NetworkMatch.m_team_scores[(int)team]
                    });
            //if (!MPTweaks.ClientHasMod(connectionId))
            //    return;

            // Process any disconnected scoreboard entries, add them to active pilot, and remove stale scoreboard objects
            foreach (var player in Overload.NetworkManager.m_Players)
            {
                // Add any previous records for pilot on join
                var playersSameName = Overload.NetworkManager.m_PlayersForScoreboard.Where(x => !String.IsNullOrEmpty(x.m_mp_name) && x.m_mp_name == player.m_mp_name).ToList();
                player.m_kills = playersSameName.Sum(x => x.m_kills);
                player.m_deaths = playersSameName.Sum(x => x.m_deaths);
                player.m_assists = playersSameName.Sum(x => x.m_assists);

                // Remove previously DC'd players of same name from server scoreboard list
                playersSameName.Where(x => x != player).ToList().ForEach(x => Overload.NetworkManager.m_PlayersForScoreboard.Remove(x));
            }

            var n = Overload.NetworkManager.m_Players.Count;
            var msg = new MatchStateMessage()
            {
                m_match_elapsed_seconds = NetworkMatch.m_match_elapsed_seconds,
                m_player_states = new PlayerMatchState[n]
            };
            int i = 0;
            foreach (var player in Overload.NetworkManager.m_Players)
                msg.m_player_states[i++] = new PlayerMatchState()
                {
                    m_net_id = player.netId,
                    m_kills = player.m_kills,
                    m_deaths = player.m_deaths,
                    m_assists = player.m_assists
                };
            NetworkServer.SendToClient(connectionId, MessageTypes.MsgSetMatchState, msg);

            // Send disconnected pilot stats as separate message
            if (MPTweaks.ClientHasTweak(connectionId, "jip"))
            {
                var dcPlayers = Overload.NetworkManager.m_PlayersForScoreboard.Where(x => !Overload.NetworkManager.m_Players.Contains(x));
                var dcmsg = new DisconnectedPlayerMatchStateMessage()
                {
                    m_player_states = new DisconnectedPlayerMatchState[dcPlayers.Count()]
                };
                int j = 0;
                foreach (var player in dcPlayers)
                {
                    dcmsg.m_player_states[j++] = new DisconnectedPlayerMatchState()
                    {
                        m_net_id = NetworkInstanceId.Invalid,
                        m_kills = player.m_kills,
                        m_deaths = player.m_deaths,
                        m_assists = player.m_assists,
                        m_mp_name = player.m_mp_name,
                        m_mp_team = player.m_mp_team
                    };
                }
                NetworkServer.SendToClient(connectionId, MessageTypes.MsgSetDisconnectedMatchState, dcmsg);
            }

            // CTF Stats
            if (MPModPrivateData.MatchMode == ExtMatchMode.CTF)
            {
                NetworkServer.SendToClient(connectionId, MessageTypes.MsgCTFPlayerStats, new CTF.PlayerStatesMessage() { m_player_states = CTF.PlayerStats });
            }

            // Monsterball Stats
            if (MPModPrivateData.MatchMode == ExtMatchMode.MONSTERBALL)
            {
                NetworkServer.SendToClient(connectionId, MessageTypes.MsgMonsterballPlayerStats, new MonsterballAddon.PlayerStatesMessage() { m_player_states = MonsterballAddon.PlayerStats });
            }
        }

        private static float SendPreGame(int connectionId, float pregameWait)
        {
            if (NetworkMatch.m_players.TryGetValue(connectionId, out PlayerLobbyData pld) &&
                pld.m_name.StartsWith("OBSERVER"))
                pregameWait = 0;
            // restore lobby_id which got wiped out in Client.OnSetLoadout
            foreach (var idData in NetworkMatch.m_player_loadout_data)
                idData.Value.lobby_id = idData.Key;
            //Debug.Log("JIP: SendLoadoutDataToClients: " + NetworkMatch.m_player_loadout_data.Join());
            IntegerMessage durationMsg = new IntegerMessage((int)(pregameWait * 1000));
            NetworkServer.SendToClient(connectionId, CustomMsgType.StartPregameCountdown, durationMsg);
            Debug.Log("JIP: sending start pregame countdown");

            return pregameWait;
        }

        private static IEnumerator MatchStart(Queue<int> connectionIds)
        {
            while (connectionIds.Count > 0)
            {
                int connectionId = connectionIds.Peek();

                var newPlayer = Server.FindPlayerByConnectionId(connectionId);
                if (newPlayer == null)
                {
                    connectionIds.Dequeue();
                    continue;
                }

                float pregameWait = 3f;

                if (!newPlayer.m_mp_name.StartsWith("OBSERVER"))
                {
                    foreach (Player player in Overload.NetworkManager.m_Players)
                    {
                        UnityEngine.Debug.LogFormat("XXXX SEND1PRE NewPlayer {0}, Player: {1}", newPlayer.netId, player.netId);
                        if (MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "jip"))
                        {
                            NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgJIPJustJoined, new JIPJustJoinedMessage { playerId = newPlayer.netId, ready = false });
                            UnityEngine.Debug.LogFormat("XXXX SEND1 NewPlayer {0}, Player: {1}, ready: false", newPlayer.netId, player.netId);
                        }
                    }
                    MPJoinInProgress.SetReady(newPlayer, false);
                }

                pregameWait = SendPreGame(connectionId, pregameWait);
                yield return new WaitForSeconds(pregameWait);

                Server.SendLoadoutDataToClients();

                if (newPlayer.m_mp_name.StartsWith("OBSERVER"))
                {
                    Debug.LogFormat("Enabling spectator for {0}", newPlayer.m_mp_name);
                    newPlayer.Networkm_spectator = true;
                    Debug.LogFormat("Enabled spectator for {0}", newPlayer.m_mp_name);

                    yield return null; // make sure spectator change is received before sending MatchStart
                }
                else
                {
                    foreach (Player player in Overload.NetworkManager.m_Players)
                    {
                        UnityEngine.Debug.LogFormat("XXXX SEND2PRE NewPlayer {0}, Player: {1}", newPlayer.netId, player.netId);
                        if (MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "jip"))
                        {
                            NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgJIPJustJoined, new JIPJustJoinedMessage { playerId = newPlayer.netId, ready = true });
                            UnityEngine.Debug.LogFormat("XXXX SEND2 NewPlayer {0}, Player: {1}, ready: true", newPlayer.netId, player.netId);
                        }
                    }
                    MPJoinInProgress.SetReady(newPlayer, true);
                }

                if (NetworkMatch.GetMatchState() != MatchState.PLAYING)
                {
                    connectionIds.Dequeue();
                    continue;
                }

                IntegerMessage modeMsg = new IntegerMessage((int)NetworkMatch.GetMode());
                NetworkServer.SendToClient(connectionId, CustomMsgType.MatchStart, modeMsg);
                SendMatchState(connectionId);

                NetworkSpawnPlayer.Respawn(newPlayer.c_player_ship);
                MPTweaks.Send(connectionId);
                //if (!newPlayer.m_spectator && RearView.MPNetworkMatchEnabled)
                //    newPlayer.CallTargetAddHUDMessage(newPlayer.connectionToClient, "REARVIEW ENABLED", -1, true);
                CTF.SendJoinUpdate(newPlayer);
                Race.SendJoinUpdate(newPlayer);
                foreach (Player player in Overload.NetworkManager.m_Players)
                {
                    if (player.connectionToClient.connectionId == connectionId)
                        continue;
                    // Resend mode for existing player to move h2h -> anarchy
                    NetworkServer.SendToClient(player.connectionToClient.connectionId, CustomMsgType.MatchStart, modeMsg);

                    if (!newPlayer.m_spectator)
                        player.CallTargetAddHUDMessage(player.connectionToClient, String.Format(Loc.LS("{0} JOINED MATCH"), newPlayer.m_mp_name), -1, true);

                    //Debug.Log("JIP: spawning on new client net " + player.netId + " lobby " + player.connectionToClient.connectionId);
                    NetworkServer.SendToClient(connectionId, CustomMsgType.Respawn, new RespawnMessage
                    {
                        m_net_id = player.netId,
                        lobby_id = player.connectionToClient.connectionId,
                        m_pos = player.transform.position,
                        m_rotation = player.transform.rotation,
                        use_loadout1 = player.m_use_loadout1
                    });
                }
                ServerStatLog.Connected(newPlayer.m_mp_name);
                connectionIds.Dequeue();
            }
        }

        private static void Postfix(NetworkMessage msg)
        {
            Server.SendLoadoutDataToClients();

            int connectionId = msg.conn.connectionId;
            if (!MPTweaks.ClientHasMod(connectionId) && MPTweaks.MatchNeedsMod())
                return;

            if (NetworkMatch.GetMatchState() == MatchState.PLAYING)
            {
                // serialize joining clients, do not allow overlapping co-routines for different clients
                MPJoinInProgress.ClientsInJoinPhase.Enqueue(connectionId);
                if (MPJoinInProgress.ClientsInJoinPhase.Count <= 1) {
                  GameManager.m_gm.StartCoroutine(MatchStart(MPJoinInProgress.ClientsInJoinPhase));
                }
            }
            else if (NetworkMatch.GetMatchState() == MatchState.PREGAME && NetworkMatch.m_pregame_countdown_active)
            {
                SendPreGame(connectionId, NetworkMatch.m_pregame_countdown_seconds_left);
            }
        }
    }

    [HarmonyPatch(typeof(Server), "OnDisconnect")]
    class JIPDisconnectRemovePing
    {
        private static FieldInfo _ServerPing_m_pings_Field = typeof(ServerPing).GetField("m_pings", BindingFlags.NonPublic | BindingFlags.Static);
        private static void Postfix(NetworkMessage msg)
        {
            var pings = _ServerPing_m_pings_Field.GetValue(null) as Dictionary<int, PingForConnection>;
            pings.Remove(msg.conn.connectionId);
        }
    }

    /*
    Fix for unable to rejoin with multiple players on same pc
    doesn't work because OnUpdateGameSession uses only (pc)playerId for dup check
    and restores m_has_connected / m_time_slot_reserved if same playerId is already connected :(
    [HarmonyPatch]
    class JIPRejoin
    {
        private static Type CPIType;
        private static Type HPMIType;

        private static MethodBase TargetMethod()
        {
            Type HAMIType = typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all);
            CPIType = HAMIType.GetNestedType("ConnectedPlayerInfo", AccessTools.all);
            HPMIType = typeof(NetworkMatch).GetNestedType("HostPlayerMatchmakerInfo", AccessTools.all); 
            return HAMIType.GetMethod("NotifyPlayerDisconnected");
        }

        public static void DisconnectConnectedPlayerInfo(object cip)
        {
            object hpmi = CPIType.GetProperty("MatchmakerInfo").GetValue(cip, null);
            Debug.LogFormat("DisconnectConnectedPlayerInfo, m_has_connected was {0}", HPMIType.GetField("m_has_connected").GetValue(hpmi));
            HPMIType.GetField("m_has_connected").SetValue(hpmi, false);
            HPMIType.GetField("m_time_slot_reserved").SetValue(hpmi, new DateTime(0)); // force timeout
            Debug.LogFormat("DisconnectConnectedPlayerInfo, m_has_connected is {0}", HPMIType.GetField("m_has_connected").GetValue(hpmi));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 = before first label, 1 = after first label
            foreach (var code in codes)
            {
                if (state == 0 && code.labels.Count != 0) // after if
                {
                    var c = new CodeInstruction(OpCodes.Ldloc_0) { labels = code.labels };
                    code.labels = new List<Label>();
                    yield return c;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JIPRejoin), "DisconnectConnectedPlayerInfo"));
                    state = 1;
                }
                yield return code;
            }
        }
    }
    */

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class JIPMatchSetup
    {
        public static void DrawMpMatchJIP(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawStringOptionItem(Loc.LS("ALLOW JOIN IN PROGRESS"), position, 7,
                MenuManager.GetToggleSetting(MPJoinInProgress.MenuManagerEnabled || MPJoinInProgress.SingleMatchEnable ? 1 : 0),
                Loc.LS("ALLOW PLAYERS TO JOIN MATCH AFTER IT HAS STARTED"), 1f, !MenuManager.m_mp_lan_match || MPJoinInProgress.SingleMatchEnable);
            position.y += 62f;
#if false // waits on olmod server detection
            uie.SelectAndDrawStringOptionItem(Loc.LS("REAR VIEW MIRROR"), position, 8,
                MenuManager.GetToggleSetting(RearView.MPMenuManagerEnabled ? 1 : 0),
                Loc.LS("ENABLE REAR VIEW MIRROR"), 1.5f, !MenuManager.m_mp_lan_match);
            position.y += 62f;
#endif
        }

        public static void DrawMpMatchCreateOpen(UIElement uie, ref Vector2 position)
        {
            if (!MenuManager.m_mp_lan_match)
                return;
            uie.SelectAndDrawItem(Loc.LS("CREATE OPEN MATCH"), position, 2, false);
            position.y += 62f;
            if (UIManager.m_menu_selection == 2)
            {
                UIElement.ToolTipActive = true;
                UIElement.ToolTipTitle = "CREATE OPEN MATCH";
                UIElement.ToolTipDescription = "CREATE MATCH AND ALLOW OTHERS TO JOIN AFTER STARTING";
            }
            else
                UIElement.ToolTipActive = false;
            uie.DrawMenuToolTip(position + Vector2.up * 40f);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var jipMatchSetup_DrawMpMatchCreateOpen_Method = AccessTools.Method(typeof(JIPMatchSetup), "DrawMpMatchCreateOpen");
            var jipMatchSetup_DrawMpMatchJIP_Method = AccessTools.Method(typeof(JIPMatchSetup), "DrawMpMatchJIP");

            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldstr && (string)code.operand == "CREATE MATCH")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, jipMatchSetup_DrawMpMatchCreateOpen_Method);
                    state = 1;
                }
                if (state == 1 && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 155f)
                    code.operand = 155f + 62f;
                if (state == 1 && code.opcode == OpCodes.Ldstr && (string)code.operand == "POWERUP SETTINGS")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, jipMatchSetup_DrawMpMatchJIP_Method);
                    state = 2;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class JIPMatchSetupHandle
    {
        static void Postfix()
        {
            if (MenuManager.m_menu_micro_state == 0)
                MPJoinInProgress.SingleMatchEnable = false;
            var prev_dir = UIManager.m_select_dir;
            if (MenuManager.m_menu_sub_state != MenuSubState.ACTIVE ||
                (!UIManager.PushedSelect(100) && (!MenuManager.option_dir || !UIManager.PushedDir())))
                return;

            if (MenuManager.m_menu_micro_state == 3 &&
                (UIManager.m_menu_selection == 7 || UIManager.m_menu_selection == 8))
            {
                if (UIManager.m_menu_selection == 7)
                    MPJoinInProgress.MenuManagerEnabled = !MPJoinInProgress.MenuManagerEnabled;
                MenuManager.PlayCycleSound(1f, (float)prev_dir);
            }
            else if (MenuManager.m_menu_micro_state == 0 && UIManager.m_menu_selection == 2) // create open match
            {
                MenuManager.m_menu_micro_state = 2;
                MenuManager.UIPulse(2f);
                MenuManager.PlaySelectSound();
                MenuManager.SetDefaultSelection(7);
                MPJoinInProgress.SingleMatchEnable = true;
            }
        }
    }

    // Send min_num_players 1 instead of 2 for creating join-in-progress match. Depends on FindPrivateMatchGrouping patch in MPMaxPlayer
    [HarmonyPatch(typeof(NetworkMatch), "StartMatchMakerRequest")]
    class JIPMinPlayerPatch
    {
        public static void FinalizeRequest(MatchmakerPlayerRequest req)
        {
            if (MenuManager.m_mp_lan_match && (MPJoinInProgress.MenuManagerEnabled || MPJoinInProgress.SingleMatchEnable) &&
                NetworkMatch.m_match_req_password == "")
                req.PlayerAttributes["min_num_players"] = 1;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var jipMinPlayerPatch_FinalizeRequest_Method = AccessTools.Method(typeof(JIPMinPlayerPatch), "FinalizeRequest");

            LocalBuilder reqVar = null;
            CodeInstruction last = null;
            foreach (var code in codes)
            {
                if (last != null && last.opcode == OpCodes.Newobj && ((ConstructorInfo)last.operand).ReflectedType == typeof(MatchmakerPlayerRequest) &&
                    (code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S))
                    reqVar = (LocalBuilder)code.operand;
                if (reqVar != null && code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "m_system_game_client")
                {
                    yield return new CodeInstruction(OpCodes.Ldloc, reqVar);
                    yield return new CodeInstruction(OpCodes.Call, jipMinPlayerPatch_FinalizeRequest_Method);
                }
                yield return code;
                last = code;
            }
        }
    }

    public class PlayerMatchState
    {
        public NetworkInstanceId m_net_id;
        public int m_kills;
        public int m_deaths;
        public int m_assists;
    }

    public class DisconnectedPlayerMatchState : PlayerMatchState
    {
        public string m_mp_name;
        public MpTeam m_mp_team;
    }

    public class MatchStateMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.Write(m_match_elapsed_seconds);
            writer.WritePackedUInt32((uint)m_player_states.Length);
            foreach (var pl_state in m_player_states)
            {
                writer.Write(pl_state.m_net_id);
                writer.WritePackedUInt32((uint)pl_state.m_kills);
                writer.WritePackedUInt32((uint)pl_state.m_deaths);
                writer.WritePackedUInt32((uint)pl_state.m_assists);
            }
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_match_elapsed_seconds = reader.ReadSingle();
            var n = reader.ReadPackedUInt32();
            m_player_states = new PlayerMatchState[n];
            for (int i = 0; i < n; i++)
                m_player_states[i] = new PlayerMatchState()
                {
                    m_net_id = reader.ReadNetworkId(),
                    m_kills = (int)reader.ReadPackedUInt32(),
                    m_deaths = (int)reader.ReadPackedUInt32(),
                    m_assists = (int)reader.ReadPackedUInt32()
                };
        }
        public float m_match_elapsed_seconds;
        public PlayerMatchState[] m_player_states;
    }

    public class DisconnectedPlayerMatchStateMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.WritePackedUInt32((uint)m_player_states.Length);
            foreach (var pl_state in m_player_states)
            {
                writer.Write(pl_state.m_net_id);
                writer.WritePackedUInt32((uint)pl_state.m_kills);
                writer.WritePackedUInt32((uint)pl_state.m_deaths);
                writer.WritePackedUInt32((uint)pl_state.m_assists);
                writer.Write(pl_state.m_mp_name);
                writer.WritePackedUInt32((uint)pl_state.m_mp_team);
            }
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            var n = reader.ReadPackedUInt32();
            m_player_states = new DisconnectedPlayerMatchState[n];
            for (int i = 0; i < n; i++)
                m_player_states[i] = new DisconnectedPlayerMatchState()
                {
                    m_net_id = reader.ReadNetworkId(),
                    m_kills = (int)reader.ReadPackedUInt32(),
                    m_deaths = (int)reader.ReadPackedUInt32(),
                    m_assists = (int)reader.ReadPackedUInt32(),
                    m_mp_name = reader.ReadString(),
                    m_mp_team = (MpTeam)reader.ReadPackedUInt32()
                };
        }
        public DisconnectedPlayerMatchState[] m_player_states;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class JIPClientHandlers
    {
        private static void OnSetMatchStateMsg(NetworkMessage msg)
        {
            var msmsg = msg.ReadMessage<MatchStateMessage>();
            NetworkMatch.m_match_elapsed_seconds = msmsg.m_match_elapsed_seconds;
            foreach (var pl_state in msmsg.m_player_states)
            {
                GameObject gameObject = ClientScene.FindLocalObject(pl_state.m_net_id);
                if (gameObject == null)
                    continue;
                var player = gameObject.GetComponent<Player>();
                player.m_kills = pl_state.m_kills;
                player.m_deaths = pl_state.m_deaths;
                player.m_assists = pl_state.m_assists;
            }
            NetworkMatch.SortAnarchyPlayerList();
        }

        private static void OnSetDisconnectedPlayerMatchStateMsg(NetworkMessage msg)
        {
            var msmsg = msg.ReadMessage<DisconnectedPlayerMatchStateMessage>();
            foreach (var pl_state in msmsg.m_player_states)
            {
                GameObject gameObject = ClientScene.FindLocalObject(pl_state.m_net_id);
                if (gameObject == null)
                {
                    // Disconnected players need instantiated client side to get added to scoreboard as proper Players
                    gameObject = UnityEngine.Object.Instantiate<GameObject>(NetworkSpawnPlayer.m_player_prefab);
                    var p = gameObject.GetComponent<Player>();
                    p.m_kills = pl_state.m_kills;
                    p.m_deaths = pl_state.m_deaths;
                    p.m_assists = pl_state.m_assists;
                    p.m_mp_name = pl_state.m_mp_name;
                    p.m_mp_team = pl_state.m_mp_team;
                    p.gameObject.SetActive(false);
                    Overload.NetworkManager.m_PlayersForScoreboard.Add(p);
                    continue;
                }
                var player = gameObject.GetComponent<Player>();
                player.m_kills = pl_state.m_kills;
                player.m_deaths = pl_state.m_deaths;
                player.m_assists = pl_state.m_assists;
            }
            NetworkMatch.SortAnarchyPlayerList();
        }

        private static void OnAddMpStatus(NetworkMessage msg)
        {
            MenuManager.AddMpStatus(msg.ReadMessage<StringMessage>().value);
        }

        public static void SendAddMpStatus(string status)
        {
            var msg = new StringMessage(status);
            foreach (var conn in NetworkServer.connections)
                if (conn != null && MPTweaks.ClientHasMod(conn.connectionId)) // do not send unsupported message to stock game
                    conn.Send(MessageTypes.MsgAddMpStatus, msg);
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(MessageTypes.MsgSetMatchState, OnSetMatchStateMsg);
            Client.GetClient().RegisterHandler(MessageTypes.MsgAddMpStatus, OnAddMpStatus);
            Client.GetClient().RegisterHandler(MessageTypes.MsgSetDisconnectedMatchState, OnSetDisconnectedPlayerMatchStateMsg);
        }
    }

    // only reset time if state actually changes (JIP resends match state as part of SendMatchState for h2h->anarchy update)
    [HarmonyPatch(typeof(NetworkMatch), "SetMatchState")]
    class JIPSetMatchState
    {
        private static bool Prefix(MatchState state)
        {
            return state != NetworkMatch.m_match_state;
        }
    }

    // Consolidate scores for multiple copies of pilot name client-side
    [HarmonyPatch(typeof(NetworkMatch), "SortAnarchyPlayerList")]
    class MPJoinInProgress_NetworkMatch_SortAnarchyPlayerList
    {
        static void Prefix()
        {
            foreach (var player in Overload.NetworkManager.m_Players)
            {
                // Add any previous records for pilot on join
                var playersSameName = Overload.NetworkManager.m_PlayersForScoreboard.Where(x => !String.IsNullOrEmpty(x.m_mp_name) && x.m_mp_name == player.m_mp_name).ToList();

                if (playersSameName.Count > 1)
                {
                    player.m_kills = playersSameName.Sum(x => x.m_kills);
                    player.m_deaths = playersSameName.Sum(x => x.m_deaths);
                    player.m_assists = playersSameName.Sum(x => x.m_assists);

                    // Remove previously DC'd players from server scoreboard list
                    playersSameName.Where(x => x != player).ToList().ForEach(x => Overload.NetworkManager.m_PlayersForScoreboard.Remove(x));
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "TargetCreateDisconnectFlash")]
    class MPJoinInProgress_PlayerShip_TargetCreateDisconnectFlash
    {
        static void Postfix(PlayerShip __instance)
        {
            if (NetworkMatch.InGameplay())
            {
                __instance.gameObject.SetActive(false);
                RUtility.DestroyGameObjectDelayed(__instance.gameObject, true, 0);
            }
        }
    }

}
