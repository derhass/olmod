﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    public class MPChatCommand {
        // Enumeration of all defined Commands
        public enum Command {
            None, // not a known command
            // List of Commands
            Auth,
            Kick,
            Ban,
            KickBan,
            Unban,
        }

        // properties:
        Command cmd;
        public string cmdName;
        public string arg;
        public int sender_conn;
        public bool needAuth;
        public bool inLobby;

        // this Dictionary contains the set of authenticated players
        // Authentication is done based on Player.m_player_id / PlayerLobbyData.m_player_id
        private static Dictionary<string,bool> authenticatedConnections = new Dictionary<string,bool>();

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
            if (cmdName == "A" || cmdName == "AUTH") {
                cmd = Command.Auth;
            } else if (cmdName == "K" || cmdName == "KICK") {
                cmd = Command.Kick;
                needAuth = true;
            } else if (cmdName == "B" || cmdName == "BAN") {
                cmd = Command.Ban;
                needAuth = true;
            } else if (cmdName == "KB" || cmdName == "KICKBAN") {
                cmd = Command.KickBan;
                needAuth = true;
            } else if (cmdName == "UB" || cmdName == "UNBAN") {
                cmd = Command.Unban;
                needAuth = true;
            }
        }

        // Execute a command: Returns true if the caller should forward the chat message
        // to the clients, and false if not (when it was a special command for the server)
        public bool Execute() {
            if (cmd == Command.None) {
                return true;
            }
            Debug.LogFormat("CHATCMD {0}: {1} {2}", cmd, cmdName, arg);
            if (needAuth) {
                if (!CheckPermission()) {
                    ReturnToSender("You do not have the permission for this command!");
                    Debug.LogFormat("CHATCMD {0}: client is not authenticated!", cmd);
                    return false;
                }
            }
            bool result = false;
            switch (cmd) {
                case Command.Auth:
                    result = DoAuth();
                    break;
                case Command.Kick:
                    result = DoKickBan(true, false);
                    break;
                case Command.Ban:
                    result = DoKickBan(false, true);
                    break;
                case Command.KickBan:
                    result = DoKickBan(true, true);
                    break;
                case Command.Unban:
                    result = DoUnban();
                    break;
                default:
                    Debug.LogFormat("CHATCMD {0}: {1} {2} was not handled by server", cmd, cmdName, arg);
                    result = true; // treat it as normal chat message
                    break;
            }
            return result;
        }

        // Execute the AUTH command
        public bool DoAuth() {
            string id = FindPlayerIDForConnection(sender_conn, inLobby);
            if (id.Length < 1) {
                Debug.LogFormat("AUTH: could not determine client's player ID!");
                return false;
            }

            // TODO: get the auth password from somewhere...
            if (arg != null && arg.ToUpper() == "ABCDE") {
                Debug.LogFormat("AUTH: client {0} is authenticated", id);
                ReturnToSender("Authentication successfull.");
                if (!authenticatedConnections.ContainsKey(id)) {
                    authenticatedConnections.Add(id, true);
                }
            } else {
                // de-auth
                Debug.LogFormat("AUTH: client {0} is NOT authenticated: {1} is wrong", id, arg);
                ReturnToSender("Authentication failed.");
                if (authenticatedConnections.ContainsKey(id)) {
                    authenticatedConnections.Remove(id);
                }
            }
            return false;
        }

        // Execute KICK or BAN command
        public bool DoKickBan(bool doKick, bool doBan) {
            string op;
            if (doKick && doBan) {
                op = "KICKBAN";
            } else if (doKick) {
                op = "KICK";
            } else if (doBan) {
                op = "BAN";
            } else {
                return false;
            }

            Debug.LogFormat("{0} request for {1}", op, arg);
            if (inLobby) {
                PlayerLobbyData p = FindPlayerInLobby();
                if (p == null) {
                    Debug.LogFormat("{0}: no player {1} found in LOBBY", op, arg);
                    ReturnToSender(String.Format("{0}: player {1} not found in LOBBY",op, arg));
                } else {
                    if (doBan) {
                        Debug.LogFormat("{0}: banning player {1} (while in LOBBY)", op, p.m_name);
                        MPBanPlayers.Ban(new MPBanEntry(p));
                        ReturnToAll(String.Format("player {0} has been BANNED", p.m_name), p.m_id);
                    }
                    if (doKick) {
                        Debug.LogFormat("{0}: kicking player {1} from LOBBY", op, p.m_name);
                        ReturnToAll(String.Format("player {0} has been KICKED", p.m_name), p.m_id);
                        if (p.m_id < NetworkServer.connections.Count && NetworkServer.connections[p.m_id] != null) {
                            NetworkServer.connections[p.m_id].Disconnect();
                        }
                    }
                }
            } else {
                Player p = FindPlayer();
                if (p == null) {
                    Debug.LogFormat("{0}: no player {1} found in GAME", op, arg);
                    ReturnToSender(String.Format("{0}: player {1} not found in GAME",op, arg));
                } else {
                    if (doBan) {
                        Debug.LogFormat("{0}: banning player {1} (while in GAME)", op, p.m_mp_name);
                        MPBanPlayers.Ban(new MPBanEntry(p));
                        ReturnToAll(String.Format("player {0} has been BANNED", p.m_mp_name), (p.connectionToClient!=null)?p.connectionToClient.connectionId:-1);
                    }
                    if (doKick) {
                        Debug.LogFormat("{0}: kicking player {1} from GAME", op, p.m_mp_name);
                        ReturnToAll(String.Format("player {0} has been BANNED", p.m_mp_name), (p.connectionToClient!=null)?p.connectionToClient.connectionId:-1);
                        p.connectionToClient.Disconnect();
                    }
                }
            }
            return false;
        }

        public bool DoUnban()
        {
            // TODO: currently, we can only unban all
            MPBanPlayers.UnbanAll();
            ReturnToAll("BAN list has been cleared");
            return false;
        }

        // Send a chat message back to the sender of the command
        // HA: An Elvis reference!
        public bool ReturnToSender(string msg) {
            if (sender_conn >= NetworkServer.connections.Count ||  NetworkServer.connections[sender_conn] == null) {
                return false;
            }
            if (inLobby) {
                var lmsg = new LobbyChatMessage(sender_conn, "server", MpTeam.TEAM0, msg, false);
                NetworkServer.SendToClient(sender_conn, 75, lmsg);
                return true;
            } else {
                foreach (var p in Overload.NetworkManager.m_Players) {
                    if (p != null && p.connectionToClient != null && p.connectionToClient.connectionId == sender_conn) {
                        p.CallTargetSendFullChat(p.connectionToClient, sender_conn, "server", MpTeam.TEAM0, msg);
                        return true;
                    }
                }
            }
            return false;
        }

        // Send a chat message to all clients except the given connection id, -1 for all
        public bool ReturnToAll(string msg, int connId = -1) {
            if (inLobby) {
                var lmsg = new LobbyChatMessage(sender_conn, "server", MpTeam.TEAM0, msg, false);
                if (connId < 0) {
                    NetworkServer.SendToAll(75, lmsg);
                } else {
                    foreach(NetworkConnection c in NetworkServer.connections) {
                        if (c != null && c.connectionId != connId) {
                            NetworkServer.SendToClient(c.connectionId, 75, lmsg);
                        }
                    }
                }
                return true;
            } else {
                foreach (var p in Overload.NetworkManager.m_Players) {
                    if (p != null && p.connectionToClient != null && p.connectionToClient.connectionId != connId) {
                        p.CallTargetSendFullChat(p.connectionToClient, sender_conn, "server", MpTeam.TEAM0, msg);
                        return true;
                    }
                }
            }
            return false;
        }

        // Find the player ID string based on a connection ID
        public string FindPlayerIDForConnection(int conn_id, bool inLobby) {
            if (inLobby) {
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    if (p.Value != null && p.Value.m_id == conn_id) {
                        return p.Value.m_player_id;
                    }
                }
            } else {
                foreach (var p in Overload.NetworkManager.m_Players) {
                    if (p != null && p.connectionToClient != null && p.connectionToClient.connectionId == conn_id) {
                        return p.m_mp_player_id;
                    }
                }
            }
            return "";
        }

        // Check if the sender of the message is authenticated
        public bool CheckPermission() {
            string id = FindPlayerIDForConnection(sender_conn, inLobby);
            if (id.Length < 1) {
                Debug.LogFormat("CHATCMD: could not determine client's player ID!");
                return false;
            }
            return (authenticatedConnections.ContainsKey(id) && authenticatedConnections[id] == true);
        }

        // Match a player name versus the player name pattern
        // Return 1 on perfect match
        //        0 on no match at all
        // or a negative value with lower value meaning worse match
        public int MatchPlayerName(string name, string pattern)
        {
            if (name == pattern) {
                // perfect match
                return 1;
            }

            if (name.Contains(pattern)) {
               int extraChars = name.Length - pattern.Length + 1;
               // the less extra chars, the better the match is
               return -extraChars;
            }
            return 0;
        }

        // Find the best match for a player in the arg field
        // Search the active players in game
        // May return null if no match can be found
        public Player FindPlayer() {
            if (arg == null || arg.Length < 1) {
                return null;
            }

            int bestScore = -1000000000;
            Player bestPlayer = null;
            string pattern = arg.ToUpper();

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

        // Find the best match for a player in the arg field
        // Search the active players in the lobby
        // May return null if no match can be found
        public PlayerLobbyData FindPlayerInLobby() {
            if (arg == null || arg.Length < 1) {
                return null;
            }

            int bestScore = -1000000000;
            PlayerLobbyData bestPlayer = null;
            string pattern = arg.ToUpper();

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
}
