﻿using Harmony;
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
    ///  Author:  luponix 
    ///  Created: 2021-07-22
    /// </summary>
    class JoystickRotationFix
    {

        public static bool alt_turn_ramp_mode = false;          // defines which behaviour the local client wants to use, gets loaded/saved through MPSetup.cs in .xprefsmod
        public static Dictionary<uint, int> client_settings = new Dictionary<uint, int>();    // used to store the turn ramp mode setting of the active players on the server side
        public static bool server_support = true;               // indicates wether the current server supports the changed behaviour, has to be true outside of games to make the ui option accessible



        [HarmonyPatch(typeof(GameManager), "Start")]
        internal class CommandsAndInitialisationPatch10
        {
            private static void Postfix(GameManager __instance)
            {
                uConsole.RegisterCommand("debuginput", "", new uConsole.DebugCommand(CommandsAndInitialisationPatch10.CmdToggleDebugInput));
            }

            private static void CmdToggleDebugInput()
            {
                JoystickCurveEditor.DebugOutput.show = !JoystickCurveEditor.DebugOutput.show;
            }
        }



        // Debug 
        /*
        [HarmonyPatch(typeof(Player), "CmdSendFullChat")]
        internal class JoystickRotationFix_Debug_CmdSendFullChat
        {
            static void Postfix(int sender_connection_id, string sender_name, MpTeam sender_team, string msg)
            {
                if (msg.ToString().ToUpper().StartsWith("SHOW_JFS"))
                {
                    string msg3 = "empty settings";
                    if (client_settings != null)
                    {
                        msg3 = "";
                        foreach (KeyValuePair<uint, int> entry in client_settings)
                        {
                            msg3 += "  Key:" + entry.Key + "  Value:" + entry.Value;
                        }
                    }
                    LobbyChatMessage msg2 = new LobbyChatMessage(sender_connection_id, "SERVER", MpTeam.ANARCHY, msg3, false);
                    NetworkServer.SendToAll(75, msg2);
                }
            }
        }*/



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
                //Vmax = F / (mass * drag)  player.rigidbody.drag = 5.5f
                return MaybeChangeRotation() ? PlayerShip.m_ramp_max[_inst.c_player.m_player_control_options.opt_joy_ramp] : 1f;
            }

            public static bool MaybeChangeRotation()
            {
                // (Server) if this is the server lookup the current players setting with _inst
                if (GameplayManager.IsDedicatedServer())
                {
                    if (client_settings.ContainsKey(_inst.netId.Value))
                    {
                        client_settings.TryGetValue(_inst.netId.Value, out int val);
                        return val == 1;
                    }
                    return false;
                }
                // (Client)
                return alt_turn_ramp_mode && server_support;
            }
        }

        public class SetTurnRampModeMessage : MessageBase
        {
            public int mode;
            public uint netId;
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(mode);
                writer.Write(netId);
            }
            public override void Deserialize(NetworkReader reader)
            {
                mode = reader.ReadInt32();
                netId = reader.ReadUInt32();
            }
        }

        // reset the stored client settings after each match and make sure that the menu option on the clients is enabled
        


        /// ///////////////////////////////////// SERVER ///////////////////////////////////// ///
        [HarmonyPatch(typeof(Server), "RegisterHandlers")]
        class JoystickRotationFix_Server_RegisterHandlers
        {
            private static void OnSetJoystickRampMode(NetworkMessage rawMsg)
            {
                var msg = rawMsg.ReadMessage<SetTurnRampModeMessage>();
                if (client_settings.ContainsKey(msg.netId)) client_settings.Remove(msg.netId);
                client_settings.Add(msg.netId, msg.mode);
            }

            static void Postfix()
            {
                if (GameplayManager.IsDedicatedServer())
                {
                    NetworkServer.RegisterHandler(MessageTypes.MsgSetTurnRampMode, OnSetJoystickRampMode);
                }
            }
        }

        [HarmonyPatch(typeof(Server), "SendPostgameToAllClients")]
        class JoystickRotationFix_Server_SendPostgameToAllClients
        {
            private static void Postfix()
            {
                client_settings.Clear();
            }
        }


        /// ///////////////////////////////////// CLIENT ///////////////////////////////////// ///
        [HarmonyPatch(typeof(Client), "OnMatchStart")] //SendPlayerLoadoutToServer
        class JoystickRotationFix_SendPlayerLoadoutToServer
        {
            private static void Postfix()
            {
                if (GameplayManager.IsDedicatedServer()) return;
                if (Client.GetClient() == null)
                {
                    Debug.Log("JoystickRamping_SendPlayerLoadoutToServer: no client?");
                    return;
                }
                NetworkIdentity ni = GameManager.m_local_player.GetComponent<NetworkIdentity>();
                uConsole.Log("----------> just sent "+ GameManager.m_local_player.netId.Value +", " +alt_turn_ramp_mode+"   "+ ni.netId.Value +"  <----------");
                Client.GetClient().Send(MessageTypes.MsgSetTurnRampMode, 
                    new SetTurnRampModeMessage { 
                        mode = alt_turn_ramp_mode ? 1 : 0, 
                        netId = ni.netId.Value
                    });
            }
        }

        [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
        internal class JoystickRotationFix_NetworkMatch_ExitMatch
        {
            static void Postfix()
            {
                server_support = true;
            }
        }

        [HarmonyPatch(typeof(Client), "Disconnect")]
        class JoystickRotationFix_Client_Disconnect
        {
            private static void Postfix()
            {
                server_support = true;
            }
        }

        /// ///////////////////////////////////// CLIENT UI ///////////////////////////////////// ///
        [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
        internal class JoystickRotationFix_DrawControlsMenu
        {
            private static void DrawTurnspeedModeOption(UIElement uie, ref Vector2 position)
            {
                if (!server_support && !GameplayManager.IsMultiplayerActive && NetworkMatch.GetMatchState() != MatchState.LOBBY)
                {
                    server_support = true;
                    Debug.Log("JoystickRamping.server_support didnt reset properly");
                }
                position.y += 62f;
                uie.SelectAndDrawStringOptionItem(Loc.LS("TURN RAMP MODE"), position, 0, alt_turn_ramp_mode ? "LINEAR" : "DEFAULT", Loc.LS(""), 1.5f, !server_support);
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
                    //Debug.Log("Just sent the updated flag to the server: current: " + (alt_turn_ramp_mode ? "LINEAR" : "DEFAULT"));
                    Client.GetClient().Send(MessageTypes.MsgSetTurnRampMode, new SetTurnRampModeMessage { mode = alt_turn_ramp_mode ? 1 : 0, netId = GameManager.m_local_player.netId.Value });
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


