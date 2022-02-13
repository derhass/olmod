using System;
using System.Collections.Generic;
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
            Kick,
            Ban,
        }

        // properties:
        Command cmd;
        public string cmdName;
        public string arg;
        public int sender_conn;

        // Construct a MPChatCommand from a Chat message
        public MPChatCommand(string message, int sender_connection_id) {
            cmd = Command.None;
            sender_conn = sender_connection_id;

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

            /// detect the command
            cmdName = cmdName.ToUpper();
            if (cmdName == "KICK") {
                cmd = Command.Kick;
            }

        }

        // Execute a message: Returns true if the caller should forward the chat message
        // to the clients, and false if not (when it was a special command for the server)
        public bool Execute(bool inLobby) {
            if (cmd == null || cmd == Command.None) {
                return true;
            }
            Debug.LogFormat("CHATCMD {0}: {1} {2}", cmd, cmdName, arg);
            bool result = false;
            switch (cmd) {
                case Command.Kick:
                    result = DoKick(inLobby);
                    break;
                default:
                    Debug.LogFormat("CHATCMD {0}: {1} {2} was not handled by server", cmd, cmdName, arg);
                    result = true; // treat it as normal chat message
                    break;
            }
            return result;
        }

        // Execute the KICK command
        public bool DoKick(bool inLobby) {
            Debug.LogFormat("KICK request for {0}", arg);
            // TODO: manage kick permissions? E.g only the player who hosted it?
            if (inLobby) {
                // TODO: implement in-Lobby player name matching and Kicking
            } else {
                Player p = FindPlayer();
                if (p == null) {
                    Debug.LogFormat("KICK {0}: no player found", arg);
                } else {
                    Debug.LogFormat("KICK {0}: kicking player {1}", arg, p.m_mp_name);
                    p.connectionToClient.Disconnect();
                }
            }
            return false;
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
    }

    [HarmonyPatch(typeof(NetworkMatch), "ProcessLobbyChatMessageOnServer")]
    class MPChatCommands_ProcessLobbyMessage {
        private static bool Prefix(int sender_connection_id, LobbyChatMessage msg) {
            MPChatCommand cmd = new MPChatCommand(msg.m_text, sender_connection_id);
            return cmd.Execute(true);
        }
    }

    [HarmonyPatch(typeof(Player), "CmdSendFullChat")]
    class MPChatCommands_ProcessIngameMessage {
        private static bool Prefix(int sender_connection_id, string sender_name, MpTeam sender_team, string msg) {
            MPChatCommand cmd = new MPChatCommand(msg, sender_connection_id);
            return cmd.Execute(false);
        }
    }
}
