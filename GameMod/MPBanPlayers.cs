﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod {
    // the set of ban modes we support
    public enum MPBanMode : int {
        Ban=0,
        Annoy,
        Count // End Marker, always add before
    }

    // Class for entries of the ban list
    // also used for candidate entries to check ban lists against
    public class MPBanEntry {
        public string name = null;
        public string address = null;
        public string id = null;
        public bool permanent = false;
        public DateTime timeCreated;

        // generate MPBanEntry from individual entries
        public MPBanEntry(string playerName, string connAddress, string playerId) {
            Set(playerName, connAddress, playerId);
        }

        // generate MPBanEntry from name, connection_id and id
        public MPBanEntry(string playerName, int connection_id, string playerId) {
            Set(playerName, connection_id, playerId);
        }

        // generate MPBanEntry from a Player
        public MPBanEntry(Player p) {
            if (p != null) {
                Set(p.m_mp_name, (p.connectionToClient != null)?p.connectionToClient.address:null, p.m_mp_player_id);
            }
        }

        // generate MPBanEntry from a PlayerLobbyData
        public MPBanEntry(PlayerLobbyData p) {
            if (p != null) {
                Set(p.m_name, p.m_id, p.m_player_id);
            }
        }

        // Set MPBanEntry from individual entries
        public void Set(string playerName, string connAddress, string playerId) {
            name = (String.IsNullOrEmpty(playerName))?null:playerName.ToUpper();
            address = (String.IsNullOrEmpty(connAddress))?null:connAddress.ToUpper().Trim();
            id = (String.IsNullOrEmpty(playerId))?null:playerId.ToUpper().Trim();
            timeCreated = DateTime.Now;
        }

        // Set MPBanEntry from name, connection_id, and id
        public void Set(string playerName, int connection_id, string playerId) {
            string addr=null;
            if (connection_id >= 0 && connection_id < NetworkServer.connections.Count && NetworkServer.connections[connection_id] != null) {
                addr = NetworkServer.connections[connection_id].address;
            } else {
                Debug.LogFormat("BAN ENTRY: failed to find connection for connection ID {0}", connection_id);
            }
            Set(playerName, addr, playerId);
        }

        // Set MPBanEntry from another entry
        public void Set(MPBanEntry other) {
            Set(other.name, other.address, other.id);
            timeCreated = other.timeCreated;
        }

        // Describe the entry as human-readable string
        public string Describe() {
            return String.Format("(name {0}, addr {1}, ID {2})", name, address, id);
        }

        // check if the entry matches some player
        public bool matches(MPBanEntry candidate, string prefix="") {
            /* name isn't a good criteria, so ignore it
            if (!String.IsNullOrEmpty(name) && !String.IsNullOrEmpty(candidate.name)) {
                if (name == candidate.name) {
                    Debug.LogFormat("{0}player {1} matches ban list entry {1} by NAME", prefix, candidate.Describe(), Describe());
                    return true;
                }

            }*/
            if (!String.IsNullOrEmpty(address) && !String.IsNullOrEmpty(candidate.address)) {
                if (address == candidate.address) {
                    Debug.LogFormat("{0}player {1} matches ban list entry {2} by ADDRESS", prefix, candidate.Describe(), Describe());
                    return true;
                }
            }
            if (!String.IsNullOrEmpty(id) && !String.IsNullOrEmpty(candidate.id)) {
                if (id == candidate.id) {
                    Debug.LogFormat("{0}player {1} matches ban list entry {2} by ID", prefix, candidate.Describe(), Describe());
                    return true;
                }
            }

            // does not match
            return false;
        }
    }

    // class for managing banned players
    public class MPBanPlayers {
        // this is the Ban List
        private static List<MPBanEntry>[] banLists = new List<MPBanEntry>[(int)MPBanMode.Count];
        private static int totalBanCount=0; // Count of bans in all lists
        public static MPBanEntry MatchCreator = null; // description of the Game Creator

        // Get the ban list
        public static List<MPBanEntry> GetList(MPBanMode mode = MPBanMode.Ban) {
            int m = (int)mode;
            if (banLists[m] == null) {
                banLists[m] = new List<MPBanEntry>();
            }
            return banLists[m];
        }

        // Check if this player is banned
        public static bool IsBanned(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban) {
            var banList=GetList(mode);
            foreach (var entry in banList) {
                if (entry.matches(candidate, "BAN CHECK: ")) {
                    return true;
                }
            }
            // no ban entry matches this player..
            return false;
        }

        // Annoys a player
        public static void AnnoyPlayer(Player p)
        {
            if (p != null) {
                if (p.m_spectator == false) {
                    p.m_spectator = true;
                    Debug.LogFormat("BAN ANNOY player {0}",p.m_mp_name);
                    if (p.connectionToClient != null) {
                        MPChatTools.SendTo(String.Format("ANNOY BANNED player {0}",p.m_mp_name), -1, p.connectionToClient.connectionId);
                    }
                }
            }
        }

        // Annoys all Players which are actively annoy-banned
        // Doesn't work in the lobby
        public static void AnnoyPlayers()
        {
            if (GetList(MPBanMode.Annoy).Count < 1) {
                // no bans active
                return;
            }
            foreach(var p in Overload.NetworkManager.m_Players) {
                MPBanEntry candidate = new MPBanEntry(p);
                if (IsBanned(candidate, MPBanMode.Annoy)) {
                    AnnoyPlayer(p);
                }
            }
        }

        // Delayed disconnect on Kick
        public static IEnumerator DelayedDisconnect(int connection_id)
        {
            yield return new WaitForSecondsRealtime(1);
            if (connection_id < NetworkServer.connections.Count && NetworkServer.connections[connection_id] != null) {
                NetworkServer.connections[connection_id].Disconnect();
            }
        }

        // Kicks a player by a connection id. Also works in the Lobby
        public static void KickPlayer(int connection_id, string name="")
        {
            if (connection_id < 0 || connection_id >= NetworkServer.connections.Count) {
                return;
            }
            if (NetworkServer.connections[connection_id] != null) {
                MPChatTools.SendTo(String.Format("KICKED player {0}",name), -1, connection_id);
                //
                NetworkServer.SendToClient(connection_id,51, new IntegerMessage(2000000000));
                // SendPostGame(), prevents that the client immediately re-joins when in GAME
                // (doesn't help in Lobby)
                NetworkServer.SendToClient(connection_id,74, new IntegerMessage(0));
                // Goodbye
                //NetworkServer.connections[connection_id].Disconnect();
                // disconnect it in an instant, to give client time to execute the commands
                GameManager.m_gm.StartCoroutine(DelayedDisconnect(connection_id));
            }

        }

        // Kicks an active Player
        public static void KickPlayer(Player p)
        {
            if (p != null && p.connectionToClient != null) {
                KickPlayer(p.connectionToClient.connectionId, p.m_mp_name);
            }
        }

        // Kicks an Player in Lobby State
        public static void KickPlayer(PlayerLobbyData p)
        {
            if (p != null) {
                KickPlayer(p.m_id, p.m_name);
            }
        }

        // Kicks all players who are BANNED (Lobby and otherwise)
        public static void KickBannedPlayers()
        {
            if (GetList(MPBanMode.Ban).Count < 1) {
                // no bans active
                return;
            }
            MatchState s = NetworkMatch.GetMatchState();
            bool inLobby = (s == MatchState.LOBBY || s == MatchState.LOBBY_LOAD_COUNTDOWN);
            if (inLobby) {
                // Kick Lobby Players
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    if (p.Value != null) {
                        MPBanEntry candidate = new MPBanEntry(p.Value);
                        if (IsBanned(candidate, MPBanMode.Ban)) {
                            KickPlayer(p.Value);
                        }
                    }
                }
            } else {
                foreach(var p in Overload.NetworkManager.m_Players) {
                    MPBanEntry candidate = new MPBanEntry(p);
                    if (IsBanned(candidate, MPBanMode.Ban)) {
                        KickPlayer(p);
                    }
                }
            }
        }

        // Appy all bans to all all active players in Game
        public static void ApplyAllBans()
        {
            if (totalBanCount < 1) {
                return;
            }
            AnnoyPlayers();
            KickBannedPlayers();
        }

        // The ban list was modified
        public static void OnUpdate(MPBanMode mode, bool added)
        {
            totalBanCount = 0;
            for (MPBanMode m = (MPBanMode)0; m < MPBanMode.Count; m++) {
                totalBanCount += GetList(m).Count;
            }
            if (added) {
                if (mode == MPBanMode.Annoy) {
                    // apply Annoy bans directly, but not normal bans, as we have the specil KICK and KICKBAN commands
                    // that way, we can ban players without having them to be immediately kicked
                    AnnoyPlayers();
                }
            }
        }

        // Add a player to the Ban List
        public static bool Ban(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban, bool permanent = false)
        {
            candidate.permanent = permanent;
            var banList=GetList(mode);
            foreach (var entry in banList) {
                if (entry.matches(candidate, "BAN already matched: ")) {
                    // Update it
                    entry.Set(candidate);
                    OnUpdate(mode, true);
                    return false;
                }
            }
            banList.Add(candidate);
            Debug.LogFormat("BAN: player {0} is NOW banned in mode: {1}", candidate.Describe(), mode);
            OnUpdate(mode, true);
            return true;
        }

        // Remove all entries matching a candidate from the ban list
        public static bool Unban(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban)
        {
            var banList = GetList(mode);
            int cnt = banList.RemoveAll(entry => entry.matches(candidate,"UNBAN: "));
            if (cnt > 0) {
                OnUpdate(mode, false);
                return true;
            }
            return false;
        }

        // Remove all bans
        public static void UnbanAll(MPBanMode mode = MPBanMode.Ban) {
            var banList = GetList(mode);
            banList.Clear();
            OnUpdate(mode, false);
        }

        // Remove all non-Permanent bans
        public static void UnbanAllNonPermanent(MPBanMode mode = MPBanMode.Ban) {
            var banList = GetList(mode);
            banList.RemoveAll(entry => entry.permanent == false);
            OnUpdate(mode, false);
        }

        // Reset the bans for the next game if a new player creates it
        // Removes all non-permanent bans of all modes
        public static void Reset() {
            Debug.Log("MPBanPlayers: resetting all non-permanent bans");
            for (MPBanMode mode = (MPBanMode)0; mode < MPBanMode.Count; mode++) {
                UnbanAllNonPermanent(mode);
            }
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "AcceptNewConnection")]
    class MPBanPlayers_AcceptNewConnection {
        // Delayed end game
        public static IEnumerator DelayedEndMatch()
        {
            yield return new WaitForSecondsRealtime(3);
            NetworkMatch.End();
        }

        private static void Postfix(ref bool __result, int connection_id, int version, string player_name, string player_session_id, string player_id, string player_uid) {
            if (__result) {
                // the player has been accepted by the game's matchmaking
                MPBanEntry candidate = new MPBanEntry(player_name, connection_id, player_id);
                bool isCreator = false;
                if (!String.IsNullOrEmpty(NetworkMatch.m_name) && !String.IsNullOrEmpty(candidate.name)) {
                    string creator = NetworkMatch.m_name.Split('\0')[0].ToUpper();
                    if (creator == candidate.name) {
                        isCreator = true;
                    }
                }
                // check if player is banned
                bool isBanned = MPBanPlayers.IsBanned(candidate);
                if (isCreator && !isBanned) {
                    // also check annoy-bans, annoy-banned players shall not create matches either
                    // (although they can join games)
                    isBanned = MPBanPlayers.IsBanned(candidate, MPBanMode.Annoy);
                }
                if (isBanned) {
                    // banned player entered the lobby
                    // NOTE: we cannot just say __accept = false, because this causes all sorts of troubles later
                    MPBanPlayers.KickPlayer(connection_id, player_name);
                    if (isCreator) {
                        Debug.LogFormat("Creator for this match {0} is BANNED, ending match", candidate.name);
                        GameManager.m_gm.StartCoroutine(DelayedEndMatch());
                    }
                } else {
                    // unbanned player entered the lobby
                    if (isCreator) {
                        bool doReset = true;
                        if (MPChatCommand.CheckPermission(candidate)) {
                            Debug.Log("MPBanPlayers: same game creator as last match, or with permissions");
                            doReset = false;
                        }
                        MPBanPlayers.MatchCreator = candidate;
                        if (doReset) {
                            Debug.Log("MPBanPlayers: new game creator, resetting bans and permissions");
                            MPBanPlayers.Reset();
                            MPChatCommand.Reset();
                        } else {
                            MPChatTools.SendTo(true, "keeping bans and permissions from previous match", connection_id);
                        }
                    }
                }
            }
        }
    }

    // Apply the bans when the match starts
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    class MPBanPlayers_OnStartPlaying {
        private static void Postfix() {
            MPBanPlayers.ApplyAllBans();
        }
    }
}
