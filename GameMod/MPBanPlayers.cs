using System;
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

        // Reset the bans for the next game
        // Removes all non-permanent bans of all modes
        public static void Reset() {
            for (MPBanMode mode = (MPBanMode)0; mode < MPBanMode.Count; mode++) {
                UnbanAllNonPermanent(mode);
            }
        }

    }

    /*
    // THIS APPROACH DOES'T WORK TOO WELL, DISABLED FOR NOW
    // Apply the ban when a new player connects
    [HarmonyPatch(typeof(NetworkMatch), "AcceptNewConnection")]
    class MPBanPlayers_AcceptNewConnection {
        static FieldInfo FieldNMHostActiveInfo = null;
        static FieldInfo FieldLastGamesessionMatchmakerDatas = null;
        static FieldInfo FieldPlayerId = null;
        static FieldInfo FieldTimeSlotReserved = null;

        private static bool Prefix(ref bool __result, int connection_id, int version, string player_name, string player_session_id, string player_id, string player_uid) {
            MPBanEntry candidate = new MPBanEntry(player_name, connection_id, player_id);
            if (MPBanPlayers.IsBanned(candidate)) {
                // Player is banned
                MPChatTools.SendTo(String.Format("blocked BANNED player {0} from joining",player_name), -1, connection_id);
                __result = false;
                // hack to prevent matchmaking from waiting for this player to join...
                // This is ugly as hell as we have to iterate over a Dictionary with
                // internal Data Types, wich is intsels stored in an internal data type...
                if (FieldNMHostActiveInfo == null) {
                    FieldNMHostActiveInfo = AccessTools.Field( AccessTools.TypeByName("NetworkMatch"), "m_host_active_info");
                }
                var HAMI = FieldNMHostActiveInfo.GetValue(null);
                if (HAMI == null) {
                    Debug.LogFormat("MPBanPlayers: failed to get HAMI");
                    return false;
                }
                if (FieldLastGamesessionMatchmakerDatas == null) {
                    FieldLastGamesessionMatchmakerDatas = AccessTools.Field(HAMI.GetType(), "m_last_gamesession_matchmaker_datas");
                }
                var datas = FieldLastGamesessionMatchmakerDatas.GetValue(HAMI);
                if (datas == null) {
                    Debug.LogFormat("MPBanPlayers: failed to get matchmaker datas");
                    return false;
                }
                IDictionary dataDict = datas as IDictionary;
                if (dataDict != null) {
                    var it = dataDict.GetEnumerator();
                    while (it.MoveNext()) {
                        var data = it.Value;
                        if (FieldPlayerId == null) {
                            FieldPlayerId = AccessTools.Field(data.GetType(), "m_playerId");
                        }
                        if (FieldTimeSlotReserved == null) {
                            FieldTimeSlotReserved = AccessTools.Field(data.GetType(), "m_time_slot_reserved");
                        }
                        if (FieldPlayerId != null && FieldTimeSlotReserved != null) {
                            string id = (string)FieldPlayerId.GetValue(data);
                            if (id == player_id) {
                                // modify the timeout for this player as the entry still survises
                                // after the connection is closed, and server woudl wait for timeout (1 Minute)
                                // instead...
                                DateTime t = (DateTime)FieldTimeSlotReserved.GetValue(data);
                                t = DateTime.Now - TimeSpan.FromSeconds(57); // Timeout is 60 seconds by default, let it time out in 3 sec
                                Debug.LogFormat("BAN: timing out banned player id {0}", id);
                                FieldTimeSlotReserved.SetValue(data,t);
                            }
                        }
                    }
                }
                return false;
            }
            // run the method as intented
            return true;
        }
    }
    */

    // Apply the Annoy-Ban when the match starts
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    class MPBanPlayers_OnStartPlaying {
        private static void Postfix() {
            MPBanPlayers.ApplyAllBans();
        }
    }

    // Reset the bans when a new lobby starts
    // TODO: find a nice way to orevent banned players from creating a game on the
    //       server
    [HarmonyPatch(typeof(NetworkMatch), "NetSystemOnGameSessionStart")]
    class MPBanPlayers_OnNewGameSession {
        private static void Postfix() {
            MPBanPlayers.Reset();
        }
    }
}
