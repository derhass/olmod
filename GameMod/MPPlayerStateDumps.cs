using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Threading;
using System.Diagnostics;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
	public class MPPlayerStateDump {
		public enum Command : uint {
			NONE = 0,
			// Version 1
			ENQUEUE,
			UPDATE_BEGIN,
			UPDATE_END,
			INTERPOLATE_BEGIN,
			INTERPOLATE_END,
			LERP_BEGIN,
			LERP_END,
			FINISH,
			UPDATE_BUFFER_CONTENTS,
			LERP_PARAM,
			INTERPOLATE_PATH_01,
			INTERPOLATE_PATH_12,
			// Version 2
			NEW_ENQUEUE,
			NEW_TIME_SYNC,
			NEW_INTERPOLATE,
			NEW_PLAYER_RESULT,
			// Version 3: performance monitoring
			PERF_PROBE,
			// always add new commands at the end!
		}

		public enum PerfProbeMode : uint {
			BEGIN = 0,
			END,
			GENERIC_START,
		}

		public enum PerfProbeLocation : uint {
			GAMEMANAGER_UPDATE,
			GAMEMANAGER_FIXED_UPDATE,
		}

		public class Buffer {
			private FileStream fs;
			private MemoryStream ms;
			private BinaryWriter bw;
			private Mutex mtx;
			private bool go;
			private int matchCount;
			private const long maxMemBuffer = 256 * 1024;
			private Stopwatch stopWatch = new Stopwatch();

			public Buffer() {
				mtx = new Mutex();
				ms = new MemoryStream();
				bw = new BinaryWriter(ms);
				go = false;
				matchCount=0;
			}

			~Buffer() {
				Stop();
			}

			public void Start()
			{
				if (go) {
					Stop();
				}
				try {
					string basePath = Path.Combine(Application.persistentDataPath, "perfdump_");
					string curDateTime = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
					string name = basePath + curDateTime + "_" + matchCount + ".olmd";
					UnityEngine.Debug.Log("MPPlayerStateDump: dump started to " + name);
					fs = File.Create(name);
					ms.Position = 0;
					bw.Write((uint)4); // file format version
					bw.Write((uint)0); // size of extra header, reserved for later versions
					matchCount++;
					stopWatch.Stop();

					UnityEngine.Debug.LogFormat("MPPlayerStateDump: using {0}-resolution stopwatch, frequency: {1}Hz",
									((Stopwatch.IsHighResolution)?"high":"low"),Stopwatch.Frequency);
					stopWatch.Reset();
					stopWatch.Start();
					go = true;
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to initialize buffer file:" + e);
				}
			}

			public void Stop()
			{
				if (!go) {
					return;
				}
				try {
					bw.Write((uint)Command.FINISH);
					Flush(true);
					fs.Close();
					UnityEngine.Debug.Log("MPPlayerStateDump: dump finished");
					stopWatch.Stop();
					stopWatch.Reset();
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to stop: " + e);
				} finally {
					go = false;
				}
			}

			private void Flush(bool force)
			{
				if (force || ms.Position > maxMemBuffer) {
					UnityEngine.Debug.Log("MPPlayerStateDump: dumping " + ms.Position + " bytes");
					ms.SetLength(ms.Position);
					ms.WriteTo(fs);
					fs.Flush();
					ms.Position=0;
				}
			}

			private void WritePlayerSnapshot(ref PlayerSnapshot s)
			{
				bw.Write(s.m_net_id.Value);
				bw.Write(s.m_pos.x);
				bw.Write(s.m_pos.y);
				bw.Write(s.m_pos.z);
				bw.Write(s.m_rot.x);
				bw.Write(s.m_rot.y);
				bw.Write(s.m_rot.z);
				bw.Write(s.m_rot.w);
			}

			private void WriteNewPlayerSnapshot(ref NewPlayerSnapshot s)
			{
				bw.Write(s.m_net_id.Value);
				bw.Write(s.m_pos.x);
				bw.Write(s.m_pos.y);
				bw.Write(s.m_pos.z);
				bw.Write(s.m_rot.x);
				bw.Write(s.m_rot.y);
				bw.Write(s.m_rot.z);
				bw.Write(s.m_rot.w);
				bw.Write(s.m_vel.x);
				bw.Write(s.m_vel.y);
				bw.Write(s.m_vel.x);
				bw.Write(s.m_vrot.x);
				bw.Write(s.m_vrot.y);
				bw.Write(s.m_vrot.z);
			}

			private void WriteNewPlayerSnapshotMessage(ref NewPlayerSnapshotToClientMessage m)
			{
				bw.Write(m.m_server_timestamp);
				bw.Write(m.m_num_snapshots);
				for (int i=0; i<m.m_num_snapshots; i++) {
					WriteNewPlayerSnapshot(ref m.m_snapshots[i]);
				}
			}

			public void AddCommand(uint cmd) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write(cmd);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump command: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddCommand(uint cmd, float timestamp) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write(cmd);
					bw.Write(timestamp);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump command: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddUpdateBegin(float timestamp, float interpolTime) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.UPDATE_BEGIN);
					bw.Write(timestamp);
					bw.Write(interpolTime);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump update begin: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddUpdateEnd(float interpolTime) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.UPDATE_END);
					bw.Write(interpolTime);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump update end: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}


			public void AddSnapshot(ref PlayerSnapshotToClientMessage msg)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.ENQUEUE);
					bw.Write(Time.time);
					bw.Write(msg.m_num_snapshots);
					for (int i = 0; i<msg.m_num_snapshots; i++) {
						WritePlayerSnapshot(ref msg.m_snapshots[i]);
					}
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump snapshot: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddInterpolateBegin(float timestamp, int ping) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.INTERPOLATE_BEGIN);
					bw.Write(timestamp);
					bw.Write(ping);

					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump command: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddLerpBegin(bool wait_for_respawn, ref PlayerSnapshot A, ref PlayerSnapshot B, float t)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.LERP_BEGIN);
					int w = (wait_for_respawn)?1:0;
					bw.Write(w);
					// dumping the whole states again is redundant, but the amout of data is not that high... 
					WritePlayerSnapshot(ref A);
					WritePlayerSnapshot(ref B);
					bw.Write(t);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump lerp begin: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddLerpEnd(bool wait_for_respawn)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.LERP_END);
					int w = (wait_for_respawn)?1:0;
					bw.Write(w);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump lerp begin: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddBufferUpdateContents(ref PlayerSnapshotToClientMessage A,
							    ref PlayerSnapshotToClientMessage B,
							    ref PlayerSnapshotToClientMessage C,
							    int size, uint before)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.UPDATE_BUFFER_CONTENTS);
					bw.Write(Time.time);
					bw.Write(size);
					bw.Write(before);
					bw.Write(A.m_num_snapshots);
					for (int i = 0; i<A.m_num_snapshots; i++) {
						WritePlayerSnapshot(ref A.m_snapshots[i]);
					}
					bw.Write(B.m_num_snapshots);
					for (int i = 0; i<B.m_num_snapshots; i++) {
						WritePlayerSnapshot(ref B.m_snapshots[i]);
					}
					bw.Write(C.m_num_snapshots);
					for (int i = 0; i<C.m_num_snapshots; i++) {
						WritePlayerSnapshot(ref C.m_snapshots[i]);
					}
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump buffer update contents: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddLerpParam(float num) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.LERP_PARAM);
					bw.Write(num);

					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump lerp param: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddNewEnqueue(ref NewPlayerSnapshotToClientMessage msg, uint version)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.NEW_ENQUEUE);
					bw.Write(Time.realtimeSinceStartup);
					bw.Write(Time.time);
					bw.Write(Time.unscaledTime);
					bw.Write(Time.timeScale);
					bw.Write(NetworkMatch.m_match_elapsed_seconds);
                    // 0 and 1 is already used in older versions for the bool
                    //int iWasOld = (wasOld)?1:0;
                    //bw.Write(iWasOld);
                    version += 4; // ARGH, I'm an idiot, meant +2
                    bw.Write(version);
					WriteNewPlayerSnapshotMessage(ref msg);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump new snapshot: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddNewInterpolate(int ping, float extrapol)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.NEW_INTERPOLATE);
					bw.Write(Time.realtimeSinceStartup);
					bw.Write(Time.time);
					bw.Write(Time.unscaledTime);
					bw.Write(Time.timeScale);
					bw.Write(NetworkMatch.m_match_elapsed_seconds);
					bw.Write(ping);
					bw.Write(extrapol);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump new interpolate: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddNewTimeSync(uint place, float last_update_time, float delta)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.NEW_TIME_SYNC);
					bw.Write(place);
					bw.Write(Time.realtimeSinceStartup);
					bw.Write(Time.time);
					bw.Write(last_update_time);
					bw.Write(delta);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump new time sync: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddNewPlayerResult(uint mtype, float now, uint id, Vector3 pos, Quaternion rot)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.NEW_PLAYER_RESULT);
					bw.Write(mtype);
					bw.Write(Time.realtimeSinceStartup);
					bw.Write(Time.time);
					bw.Write(now);
					bw.Write(id);
					bw.Write(pos.x);
					bw.Write(pos.y);
					bw.Write(pos.z);
					bw.Write(rot.x);
					bw.Write(rot.y);
					bw.Write(rot.z);
					bw.Write(rot.w);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump new player result: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddPerfProbe(PerfProbeLocation loc, uint mode)
			{
				
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					double ts;
					stopWatch.Stop();
					ts = stopWatch.Elapsed.TotalSeconds;
					stopWatch.Start();
					bw.Write((uint)Command.PERF_PROBE);
					bw.Write((uint)loc);
					bw.Write(mode);
					bw.Write(ts);
					bw.Write(Time.time);
					bw.Write(Time.fixedTime);
					bw.Write(Time.realtimeSinceStartup);
					Flush(false);
				} catch (Exception e) {
					UnityEngine.Debug.Log("MPPlayerStateDump: failed to dump perf probe: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}


		}

		public static Buffer buf = new Buffer();

		/* these are not working as I had hoped...
		[HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
		class MPPlayerStateDump_InitBeforeEachMatch {
			private static void Postfix() {
				buf.Start();
			}
		}

		[HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
		class MPPlayerStateDump_ExitMatch {
			private static void Prefix() {
				UnityEngine.Debug.Log("EXIT!!!!!!!!!!!!!!!!");
				buf.Stop();
			}
		}
		*/

		[HarmonyPatch(typeof(Client), "Connect")]
		class MPPlayerStateDump_Connect {
			private static void Postfix() {
				buf.Start();
			}
		}
		[HarmonyPatch(typeof(Client), "Disconnect")]
		class MPPlayerStateDump_Disconnect {
			private static void Prefix() {
				buf.Stop();
			}
		}

		/*	
		[HarmonyPatch(typeof(Client), "OnPlayerSnapshotToClient")]
		class MPPlayerStateDump_Enqueue {
			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
				foreach (var code in codes) {
					// After the enqueue, call our own method.
					if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "Enqueue") {
						yield return code;
						yield return new CodeInstruction(OpCodes.Ldloc_0);
						yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPPlayerStateDump), "EnqueueBuffer"));
						UnityEngine.Debug.Log("Patched OnPlayerSnapshotToClient for MPPlayerStateDump");
						continue;

					}
					yield return code;
				}
			}
		}
		*/

		/*
		public static void EnqueueBuffer(PlayerSnapshotToClientMessage msg) {
			buf.AddSnapshot(ref msg);
		}
		*/

		/*

		[HarmonyPatch(typeof(Client), "UpdateInterpolationBuffer")]
		class MPPlayerStateDump_UpdateInterpolationBuffer {
			static void Prefix() {
				buf.AddUpdateBegin(Time.time,Client.m_InterpolationStartTime);
			}
			static void Postfix() {
				buf.AddUpdateEnd(Client.m_InterpolationStartTime);
			}
		}
		*/

		/*
		[HarmonyPatch(typeof(Client), "InterpolateRemotePlayers")]
		class MPPlayerStateDump_InterpolateRemotePlayers {
			static void Prefix() {
						int ping = GameManager.m_local_player.m_avg_ping_ms;
				buf.AddInterpolateBegin(Time.time, ping);
			}
			static void Postfix() {
				buf.AddCommand((uint)Command.INTERPOLATE_END);
			}
			}
		*/

		/*
		[HarmonyPatch(typeof(Player), "LerpRemotePlayer")]
		class MPPlayerStateDump_LerpRemotePlayer {
			static void Prefix(Player __instance, ref PlayerSnapshot A, ref PlayerSnapshot B, float t) {
				buf.AddLerpBegin(__instance.m_lerp_wait_for_respawn_pos,ref A,ref B,t);
			}
			static void Postfix(Player __instance) {
				buf.AddLerpEnd(__instance.m_lerp_wait_for_respawn_pos);
			}
		}
		*/

		[HarmonyPatch(typeof(GameManager), "Update")]
		class MPPlayerStateDump_GameManagerUpdate {
			static void Prefix() {
				buf.AddPerfProbe(PerfProbeLocation.GAMEMANAGER_UPDATE, (uint)PerfProbeMode.BEGIN);
			}
			static void Postfix() {
				buf.AddPerfProbe(PerfProbeLocation.GAMEMANAGER_UPDATE, (uint)PerfProbeMode.END);
			}
		}
		[HarmonyPatch(typeof(GameManager), "FixedUpdate")]
		class MPPlayerStateDump_GameManagerFixedUpdate {
			static void Prefix() {
				buf.AddPerfProbe(PerfProbeLocation.GAMEMANAGER_FIXED_UPDATE, (uint)PerfProbeMode.BEGIN);
			}
			static void Postfix() {
				buf.AddPerfProbe(PerfProbeLocation.GAMEMANAGER_FIXED_UPDATE, (uint)PerfProbeMode.END);
			}
		}
	}
}
