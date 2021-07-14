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
    ///  Goal:    providing an alternative option that allows using the speed of the turn ramping 
    ///           without the timescaling and while also not affecting existing configurations
    ///           
    ///  Author:  luponix 
    ///  Created: 2021-04-02
    /// </summary>
    class JoystickRamping
    {

        public static bool alt_turn_ramp_mode = false;          // defines which behaviour the local client wants to use, gets loaded/saved through MPSetup.cs in .xprefsmod
        public static Dictionary<uint, int> client_settings;    // used to store the turn ramp mode setting of the active players on the server side
        public static bool server_support = true;               // indicates wether the current server supports the changed behaviour, has to be true outside of games to make the ui option accessible


        [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
        internal class JoystickRamping_FixedUpdateProcessControlsInternal
        {
            static PlayerShip _inst;

            static void Prefix(PlayerShip __instance)
            {
                _inst = __instance;
            }

            static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
            {
                var joystickRamping_MaybeChangeRampingBehaviour_Method = AccessTools.Method(typeof(JoystickRamping_FixedUpdateProcessControlsInternal), "MaybeChangeRampingBehaviour");
                var joystickRamping_ReturnChangedTurn_Method = AccessTools.Method(typeof(JoystickRamping_FixedUpdateProcessControlsInternal), "ReturnChangedTurn");
                var joystickRamping_ReturnChangedPitch_Method = AccessTools.Method(typeof(JoystickRamping_FixedUpdateProcessControlsInternal), "ReturnChangedPitch");

                int n = 0;
                var codes = new List<CodeInstruction>(instructions);
                Label l = ilGen.DefineLabel();
                for (int i = 0; i < codes.Count; i++)
                {
                    // adds a branch instruction to differentiate between original and alternative behaviour
                    if (n == 0 && codes[i + 1].opcode == OpCodes.Call && (codes[i + 1].operand as MemberInfo).Name == "get_magnitude"
                        && codes[i + 4].opcode == OpCodes.Ldc_R4)
                    {
                        n++;
                        var newCodes = new[] {
                            new CodeInstruction(OpCodes.Call, joystickRamping_MaybeChangeRampingBehaviour_Method),
                            new CodeInstruction(OpCodes.Brtrue, l)
                        };
                        codes.InsertRange(i, newCodes);
                    }

                    // adds the alternative LINEAR branch that links to ReturnChangedTurn(), ReturnChangedPitch() to calculate a turn ramping value 
                    if (n == 1 && codes[i].opcode == OpCodes.Ldloc_S && codes[i + 2].opcode == OpCodes.Ldfld
                        && (codes[i + 2].operand as FieldInfo).Name == "m_turn_speed_multiplier")
                    {
                        n++;
                        Label skip = ilGen.DefineLabel();
                        var newCodes = new[] {
                            new CodeInstruction(OpCodes.Br, skip),
                            new CodeInstruction(OpCodes.Ldloc_S, 27),
                            new CodeInstruction(OpCodes.Call, joystickRamping_ReturnChangedTurn_Method ),
                            new CodeInstruction(OpCodes.Mul),
                            new CodeInstruction(OpCodes.Stloc_S, 27),
                            new CodeInstruction(OpCodes.Ldloc_S, 28),
                            new CodeInstruction(OpCodes.Call, joystickRamping_ReturnChangedPitch_Method ),
                            new CodeInstruction(OpCodes.Mul),
                            new CodeInstruction(OpCodes.Stloc_S, 28),
                            };
                        codes.InsertRange(i, newCodes);
                        codes[i + 1].labels.Add(l); // sets the entry point
                        codes[i + 9].labels.Add(skip); // label for skipping the alternative behaviour 
                    }

                }

                return codes;
            }



            public static bool MaybeChangeRampingBehaviour()
            {
                // (Server) if this is the server lookup the current players setting with _inst
                if (GameplayManager.IsDedicatedServer())
                {
                    try
                    {
                        if (client_settings.ContainsKey(_inst.c_player.netId.Value))
                        {
                            int val = 0;
                            client_settings.TryGetValue(_inst.c_player.netId.Value, out val);
                            return val == 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("Error in MaybeChangeRampingBehaviour: " + ex);
                    }
                    return false;

                }
                // (Client)
                return alt_turn_ramp_mode && server_support;
            }

            // cc_turn_vec.y = TURN
            public static float ReturnChangedTurn()
            {
                return 1f + Mathf.Abs((PlayerShip.m_ramp_max[_inst.c_player.m_player_control_options.opt_joy_ramp] - 1) * _inst.c_player.cc_turn_vec.y);
            }

            // cc_turn_vec.x = PITCH
            public static float ReturnChangedPitch()
            {
                return 1f + Mathf.Abs((PlayerShip.m_ramp_max[_inst.c_player.m_player_control_options.opt_joy_ramp] - 1) * _inst.c_player.cc_turn_vec.x);
            }
        }





        // MP CLIENT:
        [HarmonyPatch(typeof(Client), "RegisterHandlers")]
        class JoystickRamping_Client_RegisterHandlers
        {
            private static void OnServerTurnrampModeStatus(NetworkMessage rawMsg)
            {
                var msg = rawMsg.ReadMessage<ServerTurnrampModeStatus>();
                server_support = msg.value == 1 ? true : false;
            }

            static void Postfix()
            {
                if (Client.GetClient() == null)
                    return;
                server_support = false;
                Client.GetClient().RegisterHandler(MessageTypes.MsgServerSendJoystickRampModeCapability, OnServerTurnrampModeStatus);
            }
        }

        // send the flag on entering a game or lobby
        [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
        class JoystickRamping_AcceptedToLobby
        {
            private static void Postfix(AcceptedToLobbyMessage accept_msg)
            {
                if (Client.GetClient() == null)
                {
                    Debug.Log("JoystickRamping_AcceptedToLobby: no client?");
                    return;
                }
                server_support = false; 
                Client.GetClient().Send(MessageTypes.MsgSetJoystickRampMode, new TurnrampModeMessage { mode = alt_turn_ramp_mode ? 1 : 0, netId = GameManager.m_local_player.netId.Value });
            }
        }




        // MP SERVER:
        // listen for client sending the flag and store that information in a Dictionary
        [HarmonyPatch(typeof(Server), "RegisterHandlers")]
        class JoystickRamping_Server_RegisterHandlers
        {
            private static void OnSetJoystickRampMode(NetworkMessage rawMsg)
            {
                var msg = rawMsg.ReadMessage<TurnrampModeMessage>();

                if (client_settings == null) {
                    client_settings = new Dictionary<uint, int>();
                }
                if (client_settings.ContainsKey(msg.netId)) {
                    client_settings.Remove(msg.netId);
                }
                client_settings.Add(msg.netId, msg.mode);

            }

            static void Postfix()
            {
                NetworkServer.RegisterHandler(MessageTypes.MsgSetJoystickRampMode, OnSetJoystickRampMode);
            }
        }

        [HarmonyPatch(typeof(Server), "OnLoadoutDataMessage")]
        class JoystickRamping_Server_OnLoadoutDataMessage
        {
            static void Postfix(NetworkMessage msg)
            {
                var connId = msg.conn.connectionId;
                if (connId == 0) return;
                NetworkServer.SendToClient(connId, MessageTypes.MsgServerSendJoystickRampModeCapability, new ServerTurnrampModeStatus { value = 1 });
            }
        }


        // reset the stored client settings after each match
        [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
        internal class JoystickRamping_NetworkMatch_ExitMatch
        {
            static void Postfix()
            {
                client_settings = new Dictionary<uint, int>();
                server_support = true;
            }
        }

        public class TurnrampModeMessage : MessageBase
        {
            public int mode;
            public uint netId;
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write((byte)mode);
                writer.Write(netId);
            }
            public override void Deserialize(NetworkReader reader)
            {
                mode = reader.ReadByte();
                netId = reader.ReadUInt32();
            }
        }


        public class ServerTurnrampModeStatus : MessageBase
        {
            public int value;
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write((byte)value);
            }
            public override void Deserialize(NetworkReader reader)
            {
                value = reader.ReadByte();
            }
        }








        /////////////////////////////////////////////////////
        ///              UI - Changes                     ///
        /////////////////////////////////////////////////////
        public static string GetTurnRampMode()
        {
            return alt_turn_ramp_mode ? "LINEAR" : "DEFAULT";
        }

        // draws the option
        [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
        internal class JoystickRamping_DrawControlsMenu
        {
            private static void DrawTurnspeedModeOption(UIElement uie, ref Vector2 position)
            {
                if (!server_support && !GameplayManager.IsMultiplayerActive)
                {
                    server_support = true;
                    Debug.Log("JoystickRamping.server_support didnt reset properly");
                }
                position.y += 62f;
                uie.SelectAndDrawStringOptionItem(Loc.LS("TURN RAMPING MODE"), position, 0, GetTurnRampMode(),
                    Loc.LS("LINEAR ADDS THE TURNSPEED SPEED IMMEDIATLY AND PROPORTIONAL TO THE JOYSTICK INPUT"), 1.5f,
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
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoystickRamping_DrawControlsMenu), "DrawTurnspeedModeOption"))
                        };
                        codes.InsertRange(i + 9, newCodes);
                        break;
                    }
                }
                return codes;
            }
        }



        [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
        internal class JoystickRamping_ControlsOptionsUpdate
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
                    Client.GetClient().Send(MessageTypes.MsgSetJoystickRampMode, new TurnrampModeMessage { mode = alt_turn_ramp_mode ? 1 : 0, netId = GameManager.m_local_player.netId.Value });
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
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoystickRamping_ControlsOptionsUpdate), "ProcessTurnRampModeButtonPress")));
                        break;
                    }
                }
                return codes;
            }
        }





    }
}


