using Harmony;
using System.Collections.Generic;
using Overload;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using System;

namespace GameMod
{
    /// <summary>
    ///  Goal:    allowing to access the same max speed and the same speed gain that max turn ramping on high provides
    ///           
    ///           
    ///  Author:  luponix 
    ///  Created: 2021-07-22
    /// </summary>
    class JoystickRotationFix
    {

        public static bool alt_turn_ramp_mode = false;          // defines which behaviour the local client wants to use, gets loaded/saved through MPSetup.cs in .xprefsmod
        public static Dictionary<uint, int> client_settings;    // used to store the turn ramp mode setting of the active players on the server side
        public static bool server_support = true;               // indicates wether the current server supports the changed behaviour, has to be true outside of games to make the ui option accessible


        [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
        internal class JoystickRotationFix_FixedUpdateProcessControlsInternal
        {
            static PlayerShip _inst;

            static void Prefix(PlayerShip __instance)
            {
                _inst = __instance;
            }


            // only send the flag if the server supports it

            static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
            {
                var joystickRotationFix_MaybeScaleUpRotation_Method = AccessTools.Method(typeof(JoystickRotationFix_FixedUpdateProcessControlsInternal), "MaybeScaleUpRotation");

                var codes = new List<CodeInstruction>(instructions);
                int state = 0;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && ((FieldInfo)codes[i].operand).Name == "m_ramp_turn")
                    {
                        state++;
                        if (state == 5)
                        {
                            var newCodes = new[] {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Call, joystickRotationFix_MaybeScaleUpRotation_Method),
                                new CodeInstruction(OpCodes.Mul, null)
                            };
                            codes.InsertRange(i + 22, newCodes);
                            codes.InsertRange(i + 12, newCodes);

                            return codes;
                        }
                    }
                }
                return codes;

            }

            public static float MaybeScaleUpRotation(PlayerShip ps)
            {
                if (MaybeChangeRampingBehaviour(ps)) return 2.5f;
                else return 1f;
            }


            public static float ProbeFloat(float original)
            {
                uConsole.Log(original.ToString());
                return original;
            }

            public static bool MaybeChangeRampingBehaviour(PlayerShip ps)
            {
                // (Server) if this is the server lookup the current players setting with _inst
                if (GameplayManager.IsDedicatedServer())
                {
                    //return true;

                    if (client_settings == null) client_settings = new Dictionary<uint, int>();
                    try
                    {
                        NetworkIdentity ni = _inst.c_player.GetComponent<NetworkIdentity>();
                        if (client_settings.ContainsKey(ni.netId.Value))//_inst.c_player.netId.Value))
                        {
                            int val = 0;
                            client_settings.TryGetValue(ni.netId.Value, out val);
                            //Debug.Log("\n[Server] Found a key for " + ni.netId.Value + " " + _inst.c_player.m_mp_name+" returning " + (val == 1 ? "LINEAR" : "DEFAULT"));
                            return val == 1 && ps.c_player.m_player_control_options.opt_joy_ramp == 0;
                        }
                            //else
                            //{
                            //Debug.Log("\n[Server] Couldnt find a key for "+ ni.netId.Value + " " + _inst.c_player.m_mp_name + " returning DEFAULT");
                            //}
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("Error in MaybeChangeRampingBehaviour: " + ex);
                    }
                    return false;

                }
                // (Client)
                return alt_turn_ramp_mode && server_support && GameManager.m_local_player.m_player_control_options.opt_joy_ramp == 0;
            }
        }



        [HarmonyPatch(typeof(Server), "RegisterHandlers")]
        class JoystickRotationFix_Server_RegisterHandlers
        {
            private static void OnSetJoystickRampMode(NetworkMessage rawMsg)
            {
                //Debug.Log("\n\n\n[Server] Received a client flag");

                var msg = rawMsg.ReadMessage<ScaleRotationFlagMessage>();
                //Debug.Log("client NETID: "+msg.netId+" sent over NETID: "+rawMsg.conn.connectionId);
                if (client_settings == null)
                {
                    client_settings = new Dictionary<uint, int>();
                    //Debug.Log("[Server] client settings was empty, reset the dictionary");
                }
                /*
                Debug.Log("[Server] printing Dictionary:");
                foreach (KeyValuePair<uint, int> entry in client_settings)
                {
                    Debug.Log("  Key:" + entry.Key + "  Value:" + entry.Value);
                }*/

                if (client_settings.ContainsKey((uint)rawMsg.conn.connectionId))
                {
                    client_settings.Remove((uint)rawMsg.conn.connectionId);
                    //Debug.Log("[Server] recognized an existing entry and deleted it");
                }
                client_settings.Add((uint)rawMsg.conn.connectionId, msg.mode);

                /*Debug.Log("[Server] printing Dictionary:");
                foreach (KeyValuePair<uint, int> entry in client_settings)
                {
                    //Debug.Log(entry.ToString());
                    Debug.Log("  Key:" + entry.Key + "  Value:" + entry.Value);
                }*/

            }

            static void Postfix()
            {
                NetworkServer.RegisterHandler(MessageTypes.MsgSetJoystickScaleRotation, OnSetJoystickRampMode);
            }
        }


        [HarmonyPatch(typeof(Client), "SendPlayerLoadoutToServer")]
        class JoystickRotationFix_SendPlayerLoadoutToServer
        {
            private static void Postfix()
            {
                if (Client.GetClient() == null)
                {
                    Debug.Log("JoystickRamping_SendPlayerLoadoutToServer: no client?");
                    return;
                }
                server_support = false;
                Client.GetClient().Send(MessageTypes.MsgSetJoystickScaleRotation, new ScaleRotationFlagMessage { mode = alt_turn_ramp_mode ? 1 : 0 });
            }
        }



        // reset the stored client settings after each match
        [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
        internal class JoystickRotationFix_NetworkMatch_ExitMatch
        {
            static void Postfix()
            {
                client_settings = new Dictionary<uint, int>();
                server_support = true;
            }
        }

        public class ScaleRotationFlagMessage : MessageBase
        {
            public int mode;
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(mode);
            }
            public override void Deserialize(NetworkReader reader)
            {
                mode = reader.ReadInt32();
            }
        }





        /////////////////////////////////////////////////////
        ///              UI - Changes                     ///
        /////////////////////////////////////////////////////
        public static string GetTurnRampMode()
        {
            return alt_turn_ramp_mode ? "ON" : "OFF";
        }

        // draws the option
        [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
        internal class JoystickRotationFix_DrawControlsMenu
        {
            private static void DrawTurnspeedModeOption(UIElement uie, ref Vector2 position)
            {
                if (!server_support && !GameplayManager.IsMultiplayerActive)
                {
                    server_support = true;
                    Debug.Log("JoystickRamping.server_support didnt reset properly");
                }
                position.y += 62f;
                uie.SelectAndDrawStringOptionItem(Loc.LS("sth"), position, 0, GetTurnRampMode(),
                    Loc.LS("sth sth not usable along with turn ramping sth"), 1.5f,
                    !server_support || GameManager.m_local_player.m_player_control_options.opt_joy_ramp != 0); // grey out this option if the current server doesnt support this setting
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "MAX TURN RAMPING")
                    {
                        var newCodes = new[] {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldloca, 0),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoystickRotationFix_DrawControlsMenu), "DrawTurnspeedModeOption"))
                        };
                        codes.InsertRange(i + 9, newCodes);
                        break;
                    }
                }
                return codes;
            }
        }



        [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
        internal class JoystickRotationFix_ControlsOptionsUpdate
        {
            private static void ProcessTurnRampModeButtonPress()
            {
                alt_turn_ramp_mode = !alt_turn_ramp_mode;
                // also send the updated state to the server if the client is currently in a game
                if (GameplayManager.IsMultiplayerActive)
                {
                    if (Client.GetClient() == null)
                    {
                        Debug.Log("JoystickRamping_ControlsOptionsUpdate: no client?");
                        return;
                    }
                    Debug.Log("Just sent the updated flag to the server: current: " + (alt_turn_ramp_mode ? "LINEAR" : "DEFAULT"));
                    Client.GetClient().Send(MessageTypes.MsgSetJoystickScaleRotation, new ScaleRotationFlagMessage { mode = alt_turn_ramp_mode ? 1 : 0 });
                }
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldsfld && (codes[i].operand as FieldInfo).Name == "opt_primary_autoswitch")
                    {
                        // remove the button press handling of the 'PRIMARY AUTOSELECT' option
                        codes.RemoveRange(i + 1, 6);
                        // adds logic to handle button presses of the new option
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoystickRotationFix_ControlsOptionsUpdate), "ProcessTurnRampModeButtonPress")));
                        break;
                    }
                }
                return codes;
            }
        }





    }
}


