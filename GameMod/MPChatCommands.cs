﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    // Generic utility class for chat messages
    public class MPChatTools {
        // Send a chat message
        // Set connection_id to the ID of a specific client, or to -1 to send to all
        // clients except except_connection_id (if that is >= 0)
        // If authOnly is true, only send to clients which are authenticated for chat
        // commands.
        // You need to already know if we are currently in Lobby or not
        public static bool SendTo(bool inLobby, string msg, int connection_id=-1, int except_connection_id=-1, bool authOnly=false, string sender_name = "Server", MpTeam team = MpTeam.TEAM0) {
            bool toAll = (connection_id < 0);
            if (!toAll) {
                if (connection_id >= NetworkServer.connections.Count ||  NetworkServer.connections[connection_id] == null) {
                    return false;
                }
            }

            if (inLobby) {
                var lmsg = new LobbyChatMessage(connection_id, sender_name, team, msg, false);
                if (toAll) {
                    if (except_connection_id < 0 && !authOnly) {
                        NetworkServer.SendToAll(75, lmsg);
                    } else {
                        foreach(NetworkConnection c in NetworkServer.connections) {
                            if (c != null && c.connectionId != except_connection_id) {
                                if (!authOnly || MPChatCommand.CheckPermission(c.connectionId, true)) {
                                    NetworkServer.SendToClient(c.connectionId, 75, lmsg);
                                }
                            }
                        }
                    }
                } else {
                    if (!authOnly || MPChatCommand.CheckPermission(connection_id, true)) {
                        NetworkServer.SendToClient(connection_id, 75, lmsg);
                    }
                }
            } else {
                foreach (var p in Overload.NetworkManager.m_Players) {
                    if (p != null && p.connectionToClient != null) {
                        if ((toAll && p.connectionToClient.connectionId != except_connection_id) || (p.connectionToClient.connectionId == connection_id)) {
                            if (!authOnly || MPChatCommand.CheckPermission(new MPBanEntry(p))) {
                                p.CallTargetSendFullChat(p.connectionToClient, connection_id, sender_name, team, msg);
                            }
                        }
                    }
                }
            }
            return true;
        }

        // Send a chat message
        // Set connection_id to the ID of a specific client, or to -1 to send to all
        // clients except except_connection_id (if that is >= 0)
        // if onlyAuth is true, onlsy sent to clients which are authenticated for
        // chat commands
        public static bool SendTo(string msg, int connection_id=-1 , int except_connection_id=-1, bool authOnly=false, string sender_name = "Server", MpTeam team = MpTeam.TEAM0) {
            MatchState s = NetworkMatch.GetMatchState();
            if (s == MatchState.NONE || s == MatchState.LOBBY_LOADING_SCENE) {
                Debug.LogFormat("MPChatTools SendTo() called during match state {0}, ignored",s);
                return false;
            }
            bool inLobby = (s == MatchState.LOBBY || s == MatchState.LOBBY_LOAD_COUNTDOWN);
            return SendTo(inLobby, msg, connection_id, except_connection_id, authOnly, sender_name, team);
        }
    }

    // Class dealing with special chat commands
    public class MPChatCommand {
        // Enumeration of all defined Commands
        public enum Command {
            None, // not a known command
            // List of Commands
            GivePerm,
            RevokePerm,
            Kick,
            Ban,
            KickBan,
            Unban,
            Annoy,
            Unannoy,
            End,
            Start,
            Status,
            Test,
        }

        // properties:
        Command cmd;
        public string cmdName;
        public string arg;
        public int sender_conn;
        public bool needAuth;
        public bool inLobby;
        public MPBanEntry selectedPlayerEntry = null;
        public Player selectedPlayer = null;
        public PlayerLobbyData selectedPlayerLobbyData = null;
        public int selectedPlayerConnectionId = -1;

        // this Dictionary contains the set of authenticated players
        // Authentication is done based on Player.m_player_id / PlayerLobbyData.m_player_id
        private static Dictionary<string,bool> authenticatedConnections = new Dictionary<string,bool>();

        // Check if chat commands are enabled.
        public static bool IsEnabled()
        {
            // always enable them
            // we might add some command-line argument to disable them, if a server op doesn't want this
            return true;
        }

        // Construct a MPChatCommand from a Chat message
        public MPChatCommand(string message, int sender_connection_id, bool isInLobby) {
            cmd = Command.None;
            sender_conn = sender_connection_id;
            needAuth = false;
            inLobby = isInLobby;

            if (message == null || message.Length < 2 || message[0] != '/') {
                // not a valid command
                return;
            }

            // is there an additonal argument to this command?
            // Arguments are separated with space
            int split = message.IndexOf(' ');
            if (split > 0) {
                cmdName = message.Substring(1,split-1);
                if (split + 1 < message.Length) {
                    arg = message.Substring(split+1, message.Length - split -1);
                }
            } else {
                cmdName = message.Substring(1,message.Length - 1);
            }

            // detect the command
            cmdName = cmdName.ToUpper();
            if (cmdName == "GP" || cmdName == "GIVEPERM") {
                cmd = Command.GivePerm;
                needAuth = true;
            } else if (cmdName == "RP" || cmdName == "REVOKEPERM") {
                cmd = Command.RevokePerm;
                needAuth = true;
            } else if (cmdName == "K" || cmdName == "KICK") {
                cmd = Command.Kick;
                needAuth = true;
            } else if (cmdName == "B" || cmdName == "BAN") {
                cmd = Command.Ban;
                needAuth = true;
            } else if (cmdName == "A" || cmdName == "ANNOY") {
                cmd = Command.Annoy;
                needAuth = true;
            } else if (cmdName == "UA" || cmdName == "UNANNOY") {
                cmd = Command.Unannoy;
                needAuth = true;
            } else if (cmdName == "KB" || cmdName == "KICKBAN") {
                cmd = Command.KickBan;
                needAuth = true;
            } else if (cmdName == "UB" || cmdName == "UNBAN") {
                cmd = Command.Unban;
                needAuth = true;
            } else if (cmdName == "E" || cmdName == "END") {
                cmd = Command.End;
                needAuth = true;
            } else if (cmdName == "S" || cmdName == "START") {
                cmd = Command.Start;
                needAuth = true;
            } else if (cmdName == "STATUS") {
                cmd = Command.Status;
            } else if (cmdName == "T" || cmdName == "TEST") {
                cmd = Command.Test;
            }
        }

        // Execute a command: Returns true if the caller should forward the chat message
        // to the clients, and false if not (when it was a special command for the server)
        public bool Execute() {
            if (!IsEnabled()) {
                // chat commands are not enabled
                return true;
            }

            if (cmd == Command.None) {
                // there might be Annoy-Banned players, and we can just ignore their ramblings
                if (MPBanPlayers.GetList(MPBanMode.Annoy).Count > 0) {
                    MPBanEntry we = FindPlayerEntryForConnection(sender_conn, inLobby);
                    if (we != null && MPBanPlayers.IsBanned(we, MPBanMode.Annoy)) {
                        return false;
                    }
                }
                return true;
            }
            Debug.LogFormat("CHATCMD {0}: {1} {2}", cmd, cmdName, arg);
            if (needAuth) {
                if (!CheckPermission()) {
                    ReturnToSender(String.Format("You do not have the permission for command {0}!", cmd));
                    Debug.LogFormat("CHATCMD {0}: client is not authenticated!", cmd);
                    return false;
                }
            }
            bool result = false;
            switch (cmd) {
                case Command.GivePerm:
                    result = DoPerm(true);
                    break;
                case Command.RevokePerm:
                    result = DoPerm(false);
                    break;
                case Command.Kick:
                    result = DoKickBan(true, false, MPBanMode.Ban);
                    break;
                case Command.Ban:
                    result = DoKickBan(false, true, MPBanMode.Ban);
                    break;
                case Command.Annoy:
                    result = DoKickBan(false, true, MPBanMode.Annoy);
                    break;
                case Command.KickBan:
                    result = DoKickBan(true, true, MPBanMode.Ban);
                    break;
                case Command.Unban:
                    result = DoUnban(MPBanMode.Ban);
                    break;
                case Command.Unannoy:
                    result = DoUnban(MPBanMode.Annoy);
                    break;
                case Command.End:
                    result = DoEnd();
                    break;
                case Command.Start:
                    result = DoStart();
                    break;
                case Command.Status:
                    result = DoStatus();
                    break;
                case Command.Test:
                    result = DoTest();
                    break;
                default:
                    Debug.LogFormat("CHATCMD {0}: {1} {2} was not handled by server", cmd, cmdName, arg);
                    result = true; // treat it as normal chat message
                    break;
            }
            return result;
        }

        // set authentication status of player by id
        public static bool SetAuth(bool allowed, string id)
        {
            if (String.IsNullOrEmpty(id)) {
                Debug.LogFormat("SETAUTH callid without valid player id");
                return false;
            }

            if (allowed) {
                Debug.LogFormat("AUTH: client {0} is authenticated", id);
                if (!authenticatedConnections.ContainsKey(id)) {
                    authenticatedConnections.Add(id, true);
                }
            } else {
                // de-auth
                Debug.LogFormat("AUTH: client {0} is NOT authenticated", id);
                if (authenticatedConnections.ContainsKey(id)) {
                    authenticatedConnections.Remove(id);
                }
            }
            return true;
        }

        // Execute GIVEPERM/REVOKEPERM command
        public bool DoPerm(bool give)
        {
            string op = (give)?"GIVEPERM":"REVOKEPERM";
            if (!SelectPlayer(arg)) {
                if (give) {
                    Debug.LogFormat("{0}: no player {1} found", op, arg);
                    ReturnToSender(String.Format("{0}: player {1} not found",op, arg));
                    return false;
                } else {
                    authenticatedConnections.Clear();
                    Debug.LogFormat("{0}: all client permissions revoked", op, arg);
                    ReturnToSender(String.Format("{0}: all client permissions revoked",op));
                }
            }

            if (SetAuth(give, selectedPlayerEntry.id)) {
                ReturnToSender(String.Format("{0}: player {1} applied",op,selectedPlayerEntry.name));
                if (selectedPlayerConnectionId >= 0) {
                    ReturnTo(String.Format("You have been {0} CHAT COMMAND permissions.",(give)?"granted":"revoked"),selectedPlayerConnectionId);
                }
            } else {
                ReturnToSender(String.Format("{0}: player {1} failed",op,selectedPlayerEntry.name));
            }

            return false;
        }

        // Execute KICK or BAN or KICKBAN or ANNOY command
        public bool DoKickBan(bool doKick, bool doBan, MPBanMode banMode) {
            string op;
            string banOp = banMode.ToString().ToUpper();
            if (doKick && doBan) {
                op = "KICK" + banOp;
            } else if (doKick) {
                op = "KICK";
            } else if (doBan) {
                op = banOp;
            } else {
                return false;
            }

            Debug.LogFormat("{0} request for {1}", op, arg);
            if (!SelectPlayer(arg)) {
                Debug.LogFormat("{0}: no player {1} found", op, arg);
                ReturnToSender(String.Format("{0}: player {1} not found",op, arg));
                return false;
            }

            if(selectedPlayerConnectionId >= 0 && sender_conn == selectedPlayerConnectionId) {
                Debug.LogFormat("{0}: won't self-apply", op, arg);
                ReturnToSender(String.Format("{0}: won't self-apply",op, arg));
                return false;
            }

            if (doBan) {
                MPBanPlayers.Ban(selectedPlayerEntry, banMode);
                ReturnTo(String.Format("{0} player {1}", banOp, selectedPlayerEntry.name), -1, selectedPlayerConnectionId);
            }
            if (doKick) {
                if (selectedPlayer != null) {
                    MPBanPlayers.KickPlayer(selectedPlayer);
                } else if (selectedPlayerLobbyData != null) {
                    MPBanPlayers.KickPlayer(selectedPlayerLobbyData);
                } else {
                    MPBanPlayers.KickPlayer(selectedPlayerConnectionId, selectedPlayerEntry.name);
                }
            }
            return false;
        }

        // Execute UNBAN or UNANNOY
        public bool DoUnban(MPBanMode banMode)
        {
            if (String.IsNullOrEmpty(arg)) {
                MPBanPlayers.UnbanAll(banMode);
                ReturnTo(String.Format("ban list {0} has been cleared",banMode));
            } else {
                string pattern = arg.ToUpper();
                var banList=MPBanPlayers.GetList(banMode);
                int cnt = banList.RemoveAll(entry => (MatchPlayerName(entry.name, pattern) != 0));
                MPBanPlayers.OnUpdate(banMode, false);
                ReturnTo(String.Format("{0} players have been UNBANNED from {1} list", cnt, banMode));
            }
            return false;
        }

        // Execute END command
        public bool DoEnd()
        {
            Debug.Log("END request via chat command");
            ReturnTo("manual match END request");
            NetworkMatch.End();
            return false;
        }

        // Execute START commad
        public bool DoStart()
        {
            if (!inLobby) {
                Debug.LogFormat("START request via chat command ignored in non-LOBBY state");
                ReturnTo("START: not possible because I'm not in the Lobby");
                return false;
            }
            Debug.LogFormat("START request via chat command");
            ReturnTo("manual match START request");
            MPChatCommands_ModifyLobbyStartTime.StartMatch = true;
            return false;
        }

        // Execute STATUS command
        public bool DoStatus()
        {
            string creator;
            if (MPBanPlayers.MatchCreator != null && !String.IsNullOrEmpty(MPBanPlayers.MatchCreator.name)) {
                creator = MPBanPlayers.MatchCreator.name;
            } else {
                creator = "<UNKNOWN>";
            }

            ReturnToSender(String.Format("STATUS: {0}'s game, your auth: {1}", creator,CheckPermission()));
            ReturnToSender(String.Format("STATUS: bans: {0}, annoy-bans: {1}, authList: {2}",
                                         MPBanPlayers.GetList(MPBanMode.Ban).Count,
                                         MPBanPlayers.GetList(MPBanMode.Annoy).Count,
                                         authenticatedConnections.Count));
            return false;
        }

        // Execute TEST command
        public bool DoTest()
        {
            Debug.LogFormat("TEST request for {0}", arg);
            if (!SelectPlayer(arg)) {
                ReturnToSender(String.Format("did not find player {0}",arg));
                return false;
            }
            ReturnToSender(String.Format("found player {0}",selectedPlayerEntry.name));
            return false;
        }

        // Send a chat message back to the sender of the command
        // HA: An Elvis reference!
        public bool ReturnToSender(string msg) {
            return MPChatTools.SendTo(inLobby, msg, sender_conn);
        }

        // Send a chat message to all clients except the given connection id, -1 for all
        public bool ReturnTo(string msg, int connection_id = -1, int except_connection_id = -1, bool authOnly=false) {
            return MPChatTools.SendTo(inLobby, msg, connection_id, except_connection_id, authOnly);
        }

        // Select a player by a pattern
        public bool SelectPlayer(string pattern) {
            selectedPlayerEntry = null;
            selectedPlayer = null;
            selectedPlayerLobbyData = null;
            selectedPlayerConnectionId = -1;
            if (inLobby) {
                selectedPlayerLobbyData = FindPlayerInLobby(pattern);
                if (selectedPlayerLobbyData != null) {
                    selectedPlayerEntry = new MPBanEntry(selectedPlayerLobbyData);
                    selectedPlayerConnectionId = selectedPlayerLobbyData.m_id;
                    if (selectedPlayerConnectionId >= NetworkServer.connections.Count) {
                        selectedPlayerConnectionId = -1;
                    }
                    return true;
                }
            } else {
                selectedPlayer = FindPlayer(pattern);
                if (selectedPlayer != null) {
                    selectedPlayerEntry = new MPBanEntry(selectedPlayer);
                    selectedPlayerConnectionId = (selectedPlayer.connectionToClient!=null)?selectedPlayer.connectionToClient.connectionId:-1;
                    if (selectedPlayerConnectionId >= NetworkServer.connections.Count) {
                        selectedPlayerConnectionId = -1;
                    }
                    return true;
                }
            }
            return false;
        }

        // Find the player ID string based on a connection ID
        public static string FindPlayerIDForConnection(int conn_id, bool inLobby) {
            MPBanEntry entry = FindPlayerEntryForConnection(conn_id, inLobby);
            if (entry != null && !String.IsNullOrEmpty(entry.id)) {
                return entry.id;
            }
            return "";
        }

        // Find the player name string based on a connection ID
        public static string FindPlayerNameForConnection(int conn_id, bool inLobby) {
            MPBanEntry entry = FindPlayerEntryForConnection(conn_id, inLobby);
            if (entry != null && !String.IsNullOrEmpty(entry.name)) {
                return entry.name;
            }
            return "";
        }

        // Make a MPBanEntry candidate from a connection ID
        public static MPBanEntry FindPlayerEntryForConnection(int conn_id, bool inLobby) {
            if (inLobby) {
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    if (p.Value != null && p.Value.m_id == conn_id) {
                        return new MPBanEntry(p.Value);
                    }
                }
            } else {
                foreach (var p in Overload.NetworkManager.m_Players) {
                    if (p != null && p.connectionToClient != null && p.connectionToClient.connectionId == conn_id) {
                        return new MPBanEntry(p);
                    }
                }
            }
            return null;
        }

        // check if a specific player has chat command permissions
        public static bool CheckPermission(MPBanEntry entry) {
            if (entry == null) {
                return false;
            }
            if (MPBanPlayers.MatchCreator != null && entry.matches(MPBanPlayers.MatchCreator, "MATCH CREATOR: ")) {
                // the match creator is always authenticated
                return true;
            }

            if (String.IsNullOrEmpty(entry.id)) {
                return false;
            }
            return (authenticatedConnections.ContainsKey(entry.id) && authenticatedConnections[entry.id] == true);
        }

        // Check if a client on a specific connection id is authenticated
        public static bool CheckPermission(int connection_id, bool inLobby) {
            MPBanEntry entry = FindPlayerEntryForConnection(connection_id, inLobby);
            return CheckPermission(entry);
        }

        // Check if the sender of the message is authenticated
        public bool CheckPermission() {
            return CheckPermission(sender_conn, inLobby);
        }

        // Match string name version pattern,
        // Allow '?' as wildcard character in pattern, matches every character
        public static int MatchIndexOf(string name, string pattern) {
            int nLen = name.Length;
            int pLen = pattern.Length;
            int cnt;
            int i,j;
            cnt = nLen - pLen + 1;

            if (pLen < 1) {
                // empty pattern matches everything
                return 0;
            }

            for (i=0; i < cnt; i++) {
                bool matches = true;
                for (j=0; j<pLen; j++) {
                    if ( (pattern[j] != '?') && (pattern[j] != name[i+j])) {
                        matches = false;
                        break;
                    }
                }
                if (matches) {
                    return i;
                }
            }
            return -1;
        }

        // Match a player name versus the player name pattern
        // Return 1 on perfect match
        //        0 on no match at all
        // or a negative value with lower value meaning worse match
        public int MatchPlayerName(string name, string pattern)
        {
            if (String.IsNullOrEmpty(name)) {
                // no match possible
                return 0;
            }

            if (name == pattern) {
                // perfect match
                return 1;
            }

            int index = MatchIndexOf(name, pattern);
            if (index >= 0) {
               int extraChars = name.Length - pattern.Length + 1;
               // the earlier the match, the better is the score,
               // the less extra chars, the better the the score
               return -extraChars -(index*100);

            }
            return 0;
        }

        // Find the best match for a player
        // Search the active players in game
        // May return null if no match can be found
        public Player FindPlayer(string pattern) {
            if (String.IsNullOrEmpty(pattern)) {
                return null;
            }

            int bestScore = -1000000000;
            Player bestPlayer = null;
            pattern = pattern.ToUpper();

            foreach (var p in Overload.NetworkManager.m_Players) {
                int score = MatchPlayerName(p.m_mp_name.ToUpper(), pattern);
                if (score > 0) {
                    return p;
                }
                if (score < 0 && score > bestScore) {
                    bestScore = score;
                    bestPlayer = p;
                }
            }
            if (bestPlayer == null) {
                Debug.LogFormat("CHATCMD: did not find a player matching {0}", pattern);
            }
            return bestPlayer;
        }

        // Find the best match for a player
        // Search the active players in the lobby
        // May return null if no match can be found
        public PlayerLobbyData FindPlayerInLobby(string pattern) {
            if (String.IsNullOrEmpty(pattern)) {
                return null;
            }

            int bestScore = -1000000000;
            PlayerLobbyData bestPlayer = null;
            pattern = pattern.ToUpper();

            foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                int score = MatchPlayerName(p.Value.m_name.ToUpper(), pattern);
                if (score > 0) {
                    return p.Value;
                }
                if (score < 0 && score > bestScore) {
                    bestScore = score;
                    bestPlayer = p.Value;
                }
            }
            if (bestPlayer == null) {
                Debug.LogFormat("CHATCMD: did not find a player matching {0}", pattern);
            }
            return bestPlayer;
        }

        // Check if we have set any MPChatCommand related state
        // Currently, this check only whether the authl list is not empty
        public static bool HasModifiedState()
        {
            return (authenticatedConnections.Count > 0);
        }

        // Reset the state of the chat commands
        // This clears all authentications
        public static void Reset() {
            Debug.Log("MPChatCommands: clearing command authentications");
            authenticatedConnections.Clear();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ProcessLobbyChatMessageOnServer")]
    class MPChatCommands_ProcessLobbyMessage {
        private static bool Prefix(int sender_connection_id, LobbyChatMessage msg) {
            MPChatCommand cmd = new MPChatCommand(NetworkMessageManager.StripTeamPrefix(msg.m_text), sender_connection_id, true);
            return cmd.Execute();
        }
    }

    [HarmonyPatch(typeof(Player), "CmdSendFullChat")]
    class MPChatCommands_ProcessIngameMessage {
        private static bool Prefix(int sender_connection_id, string sender_name, MpTeam sender_team, string msg) {
            MPChatCommand cmd = new MPChatCommand(msg, sender_connection_id, false);
            return cmd.Execute();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "NetSystemHostGetLobbyInformation")]
    class MPChatCommands_ModifyLobbyStartTime {
        public static bool StartMatch = false;
        private static void Postfix(ref NetworkMatch.HostLobbyInformation __result, object hai) {
            if (hai != null && StartMatch) {
                __result.ForceMatchStartTime = DateTime.Now;
                StartMatch = false;
            }
        }
    }
}
