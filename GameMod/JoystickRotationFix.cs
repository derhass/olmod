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
    ///           EDIT: stuff is fucked, lets also make it linear, then 
    ///           
    ///  Author:  luponix 
    ///  Created: 2021-07-22
    /// </summary>
    class JoystickRotationFix
    {

        public static bool alt_turn_ramp_mode = false;          // defines which behaviour the local client wants to use, gets loaded/saved through MPSetup.cs in .xprefsmod
        public static Dictionary<uint, int> client_settings;    // used to store the turn ramp mode setting of the active players on the server side
        public static bool server_support = true;               // indicates wether the current server supports the changed behaviour, has to be true outside of games to make the ui option accessible


        // Debug
        [HarmonyPatch(typeof(Player), "CmdSendFullChat")]
        internal class JoystickRotationFix_CmdSendFullChat
        {
            static bool Prefix(int sender_connection_id, string sender_name, MpTeam sender_team, string msg)
            {
                if(msg.ToString().ToUpper().StartsWith("SHOW_JFS"))
                {
                    string msg3 = "empty settings";
                    if (client_settings != null)
                    {
                        msg3 = "";
                        foreach (KeyValuePair<uint, int> entry in client_settings)
                        {
                            msg3 +="  Key:" + entry.Key + "  Value:" + entry.Value;
                        }
                    }
                    LobbyChatMessage msg2 = new LobbyChatMessage(sender_connection_id, "SERVER", MpTeam.ANARCHY, msg3, false);
                    NetworkServer.SendToAll(75, msg2);
                    return false;
                }

                return true;
            }
        }



        [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
        internal class JoystickRotationFix_FixedUpdateProcessControlsInternal
        {
            static PlayerShip _inst;

            static void Prefix(PlayerShip __instance)
            {
                _inst = __instance;
            }

            static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
            {
                var playerShip_c_player_Field = AccessTools.Field(typeof(PlayerShip), "c_player");
                var player_cc_turn_vec_Field = AccessTools.Field(typeof(Player), "cc_turn_vec");

                var joystickRotationFix_MaybeResetVector_Method = AccessTools.Method(typeof(JoystickRotationFix_FixedUpdateProcessControlsInternal), "MaybeResetVector");
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
                            var resetNum20 = new[] {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, playerShip_c_player_Field),
                                new CodeInstruction(OpCodes.Ldflda, player_cc_turn_vec_Field),
                                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UnityEngine.Vector3), "y")),
                                new CodeInstruction(OpCodes.Call, joystickRotationFix_MaybeResetVector_Method)
                            };
                            var resetNum21 = new[] {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, playerShip_c_player_Field),
                                new CodeInstruction(OpCodes.Ldflda, player_cc_turn_vec_Field),
                                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UnityEngine.Vector3), "x")),
                                new CodeInstruction(OpCodes.Call, joystickRotationFix_MaybeResetVector_Method)
                            };
                            var adjustScaling = new[] {
                                //new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Call, joystickRotationFix_MaybeScaleUpRotation_Method),
                                new CodeInstruction(OpCodes.Mul, null)
                            };
                            codes.InsertRange(i + 22, adjustScaling);
                            codes.InsertRange(i + 14, resetNum21);
                            codes.InsertRange(i + 12, adjustScaling);
                            codes.InsertRange(i + 4, resetNum20);
                            return codes;
                        }
                    }
                }
                return codes;

            }

            public static float MaybeResetVector(float original, float changed)
            {
                return MaybeChangeRotation() ? changed : original;
            }

            public static float MaybeScaleUpRotation()
            {
                return MaybeChangeRotation() ? PlayerShip.m_ramp_max[_inst.c_player.m_player_control_options.opt_joy_ramp] : 1f;
            }



            public static float ProbeFloat(float original)
            {
                uConsole.Log(original.ToString());
                return original;
            }


            public static bool MaybeChangeRotation()
            {
                // (Server) if this is the server lookup the current players setting with _inst
                if (GameplayManager.IsDedicatedServer())
                {
                    return true;
                    /*
                    if (client_settings == null)
                    {
                        Debug.Log("client settings was empty in the maybechangerotation method");
                        client_settings = new Dictionary<uint, int>();
                    }
                    try
                    {
                        //NetworkIdentity ni = _inst.c_player.GetComponent<NetworkIdentity>();
                        if (client_settings.ContainsKey(_inst.netId.Value))//_inst.c_player.netId.Value))
                        {
                            int val = 0;
                            client_settings.TryGetValue(_inst.netId.Value, out val);
                            Debug.Log("\n[Server] Found a key for " + _inst.netId.Value + " " + _inst.c_player.m_mp_name + " returning " + (val == 1 ? "LINEAR" : "DEFAULT"));
                            return val == 1; //&& _inst.c_player.m_player_control_options.opt_joy_ramp == 0;
                        }
                        else
                        {
                            Debug.Log("\n[Server] Couldnt find a key for " + _inst.netId.Value + " " + _inst.c_player.m_mp_name + " returning DEFAULT");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("Error in MaybeChangeRampingBehaviour: " + ex);
                    }
                    return false;*/

                }
                // (Client)
                //uConsole.Log((alt_turn_ramp_mode && server_support && GameManager.m_local_player.m_player_control_options.opt_joy_ramp == 0).ToString());
                return alt_turn_ramp_mode && server_support; //&& GameManager.m_local_player.m_player_control_options.opt_joy_ramp == 0;
            }
        }



        [HarmonyPatch(typeof(Server), "RegisterHandlers")]
        class JoystickRotationFix_Server_RegisterHandlers
        {
            private static void OnSetJoystickRampMode(NetworkMessage rawMsg)
            {
                var msg = rawMsg.ReadMessage<ScaleRotationFlagMessage>();
                //Debug.Log("client NETID: "+msg.netId+" sent over NETID: "+rawMsg.conn.connectionId);
                if (client_settings == null)
                {
                    client_settings = new Dictionary<uint, int>();
                    Debug.Log("[Server] client settings was empty, reset the dictionary");
                }

                Debug.Log("[Server] printing Dictionary:");
                foreach (KeyValuePair<uint, int> entry in client_settings)
                {
                    Debug.Log("  Key:" + entry.Key + "  Value:" + entry.Value);
                }

                if (client_settings.ContainsKey((uint)rawMsg.conn.connectionId))
                {
                    client_settings.Remove((uint)rawMsg.conn.connectionId);
                    //Debug.Log("[Server] recognized an existing entry and deleted it");
                }
                client_settings.Add((uint)rawMsg.conn.connectionId, msg.mode);

                Debug.Log("[Server] printing Dictionary:");
                foreach (KeyValuePair<uint, int> entry in client_settings)
                {
                    //Debug.Log(entry.ToString());
                    Debug.Log("  Key:" + entry.Key + "  Value:" + entry.Value);
                }

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
                    Loc.LS(""), 1.5f,
                    !server_support); // grey out this option if the current server doesnt support this setting
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


