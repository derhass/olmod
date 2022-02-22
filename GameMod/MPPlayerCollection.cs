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
}
