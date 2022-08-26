using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Overload;
using UnityEngine;
namespace GameMod.Core {
    class MPPlayerCollection {
        private static FileStream logFile = null;

        public static void Close() {
            if (logFile != null) {
                logFile.Flush();
                logFile.Close();
                logFile = null;
            }
        }

        public static void Add(string id, string name) {
            try {
                if (logFile == null) {
                    logFile = new FileStream("/home/mh/tmp/DONTBACKUP/olmod-experiments/players.log",FileMode.Append,FileAccess.Write);
                }
                Byte[] data = new UTF8Encoding(true).GetBytes(String.Format("OnPlayerJoinLobbyMessage name={0}, id={1}\n",name,id));  
                logFile.Write(data, 0, data.Length);
            } catch(Exception e) {
                Debug.LogFormat("XXX {0}",e);
            }
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "SetPlayersInLobby")]
    class MPOnPlayerJoinLobbyMessageSet 
    {
        public static void Prefix(PlayerLobbyData[] data) {
            if (data == null || NetworkMatch.m_players == null) {
                return;
            }
            for(int i = 0; i<data.Length; i++) {
                PlayerLobbyData pld = data[i];
                bool found = false;
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    if (p.Value !=null && p.Value.m_player_id == pld.m_player_id) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    MPPlayerCollection.Add(pld.m_player_id, pld.m_name);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Overload.Client), "OnMatchEnd")]
    class MPOnPlayerJoinLobbyMessageReset 
    {
        public static void Postfix() {
            MPPlayerCollection.Close();
        }
    }

    class MPPlayerIdManager {
        public static string originalId = null;
        public static string currentId = null;

        public static void AssureId(string id) {
            if (String.IsNullOrEmpty(originalId)) {
                originalId = id;
            }
        }

        public static void SetRandom() {
            string id = Guid.NewGuid().ToString();
            Set(id);
        }

        public static void Set(string id) {
            if (String.IsNullOrEmpty(originalId)) {
                Debug.Log("WARNING: don't know my original ID yet");
            }
            currentId = id;
            NetworkMatch.SetPlayerId(id);
            Debug.LogFormat("my ID is now: {0}", id);
        }

        public static void SetDefault()
        {
            if (String.IsNullOrEmpty(originalId)) {
                Debug.LogFormat("sorry, don't know my ID yet");
            } else {
                currentId = null;
                Set(originalId);
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "Awake")]
    class MPPlayerIdManagerCommands
    {
        public static void cmdId()
        {
            if (uConsole.GetNumParameters() > 0) {
                MPPlayerIdManager.Set(uConsole.GetString());
            } else {
                MPPlayerIdManager.SetRandom();
            }
        }

        public static void cmdResetId()
        {
            MPPlayerIdManager.SetDefault();
        }

        public static void cmdShowId()
        {
            Debug.LogFormat("id: original: {0}, current: {1}",MPPlayerIdManager.originalId, MPPlayerIdManager.currentId);
        }

        private static void Postfix()
        {
            uConsole.RegisterCommand("setid", "sets your player id (empty for random)", cmdId);
            uConsole.RegisterCommand("resetid", "resets your player id", cmdResetId);
            uConsole.RegisterCommand("showid", "shows the player ID override settings", cmdShowId);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "SetPlayerId")]
    class MPPlayerIdManagerInject {
        private static void Prefix(ref string player_id) {
            if (String.IsNullOrEmpty(MPPlayerIdManager.currentId)) {
                MPPlayerIdManager.AssureId(player_id);
            } else {
                player_id = MPPlayerIdManager.currentId;
            }
        }
    }
}
