using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public static class MPPacketStatus
    {
        public const float interval = 3.0f; 

        private static float nextInterval=0.0f;
        
        private static int   lossInCnt = 0;
        private static int   packetCnt = 0;

        public static string status = null;
        public static Color  statusColor = UIManager.m_col_good_ping;

        public static void UpdateStatus()
        {
            if (!GameplayManager.IsDedicatedServer() && GameplayManager.IsMultiplayerActive && Client.IsConnected())
            {
                if (Time.unscaledTime >= nextInterval)
                {
                    NetworkConnection connection = Client.GetConnection();
                    int loss = NetworkTransport.GetOutgoingPacketNetworkLossPercent(connection.hostId, connection.connectionId, out _);
                    int drop = NetworkTransport.GetOutgoingPacketOverflowLossPercent(connection.hostId, connection.connectionId, out _);
                    int lossOut = loss+drop;
                    int lossNew = 0;
                    float lossIn = 0.0f;

                    int packets = NetworkTransport.GetIncomingPacketCount(connection.hostId, connection.connectionId, out _);
                    if (packets < packetCnt) {
                        packetCnt = 0;
                        lossInCnt = 0;
                    }
                    int packetsNew = packets-packetCnt;
                    if (packetsNew > 0) {
                        int lost = NetworkTransport.GetIncomingPacketLossCount(connection.hostId, connection.connectionId, out _);
                        lossNew = lost-lossInCnt;
                        lossIn = 100.0f*((float)lossNew)/((float)packetsNew);
                        lossInCnt = lost;
                        packetCnt = packets;
                    }

                    if (lossOut + lossNew > 0) {
                        statusColor = Color.Lerp(UIManager.m_col_good_ping, UIManager.m_col_em5, (lossIn + lossOut) / 20.0f);
                        status = String.Format("{0,4:0.0} {1,2}", lossIn, lossOut);
                    } else {
                        status = null;
                    }
                    nextInterval = Time.unscaledTime + interval;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Client), "Update")]
    class MPPacketStatus_Client_Update
    {
        static void Postfix()
        {
            MPPacketStatus.UpdateStatus();
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawPing")]
    class MPPacketStatus_UIElement_DrawPing
    {
        static void Postfix(Vector2 pos, UIElement __instance)
        {
            //if (MPPacketStatus.status != null) {
                pos.x += 65f;
                __instance.DrawStringSmall(MPPacketStatus.status, pos, 0.3f, StringOffset.LEFT, MPPacketStatus.statusColor, 1f);
            //}
        }
    }
}
