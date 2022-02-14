using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

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

        // Add a player to the Ban List
        public static bool Ban(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban, bool permanent = false)
        {
            candidate.permanent = permanent;
            var banList=GetList(mode);
            foreach (var entry in banList) {
                if (entry.matches(candidate, "BAN already matched: ")) {
                    // Update it
                    entry.Set(candidate);
                    return false;
                }
            }
            banList.Add(candidate);
            Debug.LogFormat("BAN: player {0} is NOW banned", candidate.Describe());
            return true;
        }

        // Remove all entries matching a candidate from the ban list
        public static bool Unban(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban)
        {
            var banList = GetList(mode);
            int cnt = banList.RemoveAll(entry => entry.matches(candidate,"UNBAN: "));
            return (cnt > 0);
        }

        // Remove all bans
        public static void UnbanAll(MPBanMode mode = MPBanMode.Ban) {
            var banList = GetList(mode);
            banList.Clear();
        }

        // Remove all non-Permanent bans
        public static void UnbanAllNonPermanent(MPBanMode mode = MPBanMode.Ban) {
            var banList = GetList(mode);
            banList.RemoveAll(entry => entry.permanent == false);
        }

    }

    [HarmonyPatch(typeof(NetworkMatch), "AcceptNewConnection")]
    class MPBanPlayers_AcceptNewConnection {
        private static bool Prefix(ref bool __result, int connection_id, int version, string player_name, string player_session_id, string player_id, string player_uid) {
            MPBanEntry candidate = new MPBanEntry(player_name, connection_id, player_id);
            if (MPBanPlayers.IsBanned(candidate)) {
                // Player is banned
                // TODO: this results in server stuck in "player joining", ARGH!!!
                MPChatTools.SendTo("blocked BANNED player {0} from joining", -1, connection_id);
                __result = false;
                return false;
            }
            // run normal AcceptNewConnection
            return true;
        }
    }

    /*
    // Fix for Server being Stuck in "player joining" Phase is client disconnects at the wrong time
    [HarmonyPatch(typeof(HostPlayerMatchmakerInfo), "GetStatus")]
    class MPBanPlayers_HostPlayerMatchmakerInfoFixPending {
        private static void Postfix(ref bool __result, ref NetworkMatch.HostPlayerMatchmakerInfo __instance) {
            if (__result == HostPlayerMatchmakerInfo.Status.Pending && __instance != null) {
                // Check if the client is actually still connected
                bool foundIt = false;
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    if (p.Value != null && p.Value.m_player_id == __instance.m_playerId) {
                        foundIt = true;
                        break;
                    }
                }
                if (!foundIt) {
                    foreach (var p in Overload.NetworkManager.m_Players) {
                        if (p != null && p.m_player_id == __instance.m_playerId) {
                            foundIt = true;
                            break;
                        }
                    }
                }
                if (!foundIt) {
                    // actually leave the pending state
                    Debug.LogFormat("Joinging Player {0} has vanished", __instance.m_playerId);
                    __instance.m_time_slot_reserved -= TimeSpan.FromSeconds(300);
                    __result = HostPlayerMatchmakerInfo.Status.TimedOut;
                }
            }
        }
    }
    */
}
