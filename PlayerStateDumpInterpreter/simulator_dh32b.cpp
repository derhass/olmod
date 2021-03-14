#include "simulator_dh32b.h"

#include "interpreter.h"
#include "math_helper.h"

#include <sstream>
#include <cmath>

namespace OlmodPlayerDumpState {
namespace Simulator {

Derhass32b::Derhass32b(ResultProcessor& rp) :
	Cow1(rp),
	mms_ship_lag_added(0.0f),
	assumeVersion(-1),
	fixedDeltaTime(1.0f/60.0f)
{
	cfg.Add(ConfigParam(mms_ship_lag_added,"ship_lag_added", "lag"));
	cfg.Add(ConfigParam(assumeVersion,"assume_snapshot_version", "sver"));

	instrp[INSTR_HARD_SYNC].name = "DH32_HARD_SYNC";
	instrp[INSTR_SOFT_SYNC].name = "DH32_SOFT_SYNC";
	instrp[INSTR_INTERPOLATE].name = "DH32_INTERPOLATE";
	instrp[INSTR_INTERPOLATE_01].name = "DH32_INTERPOLATE_01";
	instrp[INSTR_INTERPOLATE_12].name = "DH32_INTERPOLATE_12";
	instrp[INSTR_INTERPOLATE_23].name = "DH32_INTERPOLATE_23";
	instrp[INSTR_EXTRAPOLATE].name = "DH32_EXTRAPOLATE";
	instrp[INSTR_EXTRAPOLATE_PAST].name = "DH32_EXTRAPOLATE_PAST";
	instrp[INSTR_SKIP_DETECTED].name = "DH32B_SKIP_DETECTED";
	instrp[INSTR_SKIPPED_FRAMES].name = "DH32B_SKIPPED_FRAMES";
	instrp[INSTR_DUPED_FRAMES].name = "DH32B_DUPED_FRAMES";
	instrp[INSTR_UNPLAUSIBE_TIMESTAMP].name = "DH32B_UNPLAUSIBLE_TIMESTAMP";
}

Derhass32b::~Derhass32b()
{
}

const char *Derhass32b::GetBaseName() const
{
	return "derhass32";
}

void Derhass32b::EnqueueToRing(const PlayerSnapshotMessage& msg, bool estimateVelocities)
{
	m_last_messages_ring_pos_last = (m_last_messages_ring_pos_last + 1) & 3;
	m_last_messages_ring[m_last_messages_ring_pos_last] = msg;
	if (m_last_messages_ring_count < 4) {
		m_last_messages_ring_count++;
	}
	if (estimateVelocities) {
		PlayerSnapshotMessage& newMsg = m_last_messages_ring[(m_last_messages_ring_pos_last + 4) & 3];
		const PlayerSnapshotMessage& lastMsg = m_last_messages_ring[(m_last_messages_ring_pos_last + 3) & 3];
		for (size_t i=0; i<newMsg.snapshot.size(); i++) {
			PlayerSnapshot *newSnapshot=&newMsg.snapshot[i];
			const PlayerSnapshot *last=NULL;
			for (size_t j=0; j<lastMsg.snapshot.size(); j++) {
				if (lastMsg.snapshot[j].id == newSnapshot->id) {
					last = &lastMsg.snapshot[j];
					break;
				}
			}
			if (last) {
				newSnapshot->state.vel[0] = (newSnapshot->state.pos[0] - last->state.pos[0]) / fixedDeltaTime;
				newSnapshot->state.vel[1] = (newSnapshot->state.pos[1] - last->state.pos[1]) / fixedDeltaTime;
				newSnapshot->state.vel[2] = (newSnapshot->state.pos[2] - last->state.pos[2]) / fixedDeltaTime;
				// TODO: rotations
			}
		}
	}
	log.Log(Logger::DEBUG, "adding %f at %f, estimateVel: %d, have %d", msg.message_timestamp, ip->GetGameState().lastTimestamp, estimateVelocities?1:0, m_last_messages_ring_count);
}

// Clear the contents of the ring buffer
void Derhass32b::ClearRing()
{
	m_last_messages_ring_pos_last = 3;
	m_last_messages_ring_count = 0;
	m_unsynced_messages_count = 0;
	m_last_message_time = -1.0f;
	m_last_message_server_time = -1.0f;
}

// interpolate a single NewPlayerSnapshot (including the extra fields besides pos and rot)!
// this is used for generating synthetic snapshot messages in case we detected missing packets
void Derhass32b::InterpolatePlayerSnapshot(PlayerSnapshot& C, const PlayerSnapshot& A, const PlayerSnapshot& B, float t)
{
	C.id = A.id;

	lerp(A.state.pos, B.state.pos, C.state.pos, t);
	lerp(A.state.vel, B.state.vel, C.state.vel, t);
	slerp(A.state.rot, B.state.rot, C.state.rot, t);
	// TODO: vrot
	C.state.vrot[0] = B.state.vrot[0];
	C.state.vrot[1] = B.state.vrot[1];
	C.state.vrot[2] = B.state.vrot[2];
}

// extrapolate a single NewPlayerSnapshot (including the extra fields besides pos and rot)!
// this is used for generating synthetic snapshot messages in case we detected missing packets
void Derhass32b::ExtrapolatePlayerSnapshot(PlayerSnapshot& C, const PlayerSnapshot& B, float t)
{
	C.id = B.id;
	float npos[3];
	npos[0]=B.state.pos[0] + B.state.vel[0];
	npos[1]=B.state.pos[1] + B.state.vel[1];
	npos[2]=B.state.pos[2] + B.state.vel[2];

	lerp(B.state.pos, npos, C.state.pos, t);
	C.state.vel[0] = B.state.vel[0];
	C.state.vel[1] = B.state.vel[1];
	C.state.vel[2] = B.state.vel[2];
	// TODO: rotation
	C.state.rot = B.state.rot;
	C.state.vrot[0] = B.state.vrot[0];
	C.state.vrot[1] = B.state.vrot[1];
	C.state.vrot[2] = B.state.vrot[2];
}

// interpolate a whole NewPlayerSnapshotToClientMessage
// interpolate between A and B, t is the relative factor between both
// If a player is not in both messages, it will not be in the resulting messages
// this is used for generating synthetic snapshot messages in case we detected missing packets
PlayerSnapshotMessage Derhass32b::InterpolatePlayerSnapshotMessage(const PlayerSnapshotMessage& A, const PlayerSnapshotMessage& B, float t)
{
	PlayerSnapshotMessage C = B;
	size_t i,j;

	for (i=0; i<A.snapshot.size(); i++) {
		for (j=0; j<B.snapshot.size(); j++) {
			if (A.snapshot[i].id == B.snapshot[j].id) {
				InterpolatePlayerSnapshot(C.snapshot[j], A.snapshot[i], B.snapshot[j], t);
				continue;
			}
    		}
	}

	C.message_timestamp = (1.0f - t) * A.message_timestamp + t*B.message_timestamp;
	return C;
}

// extrapolate a whole NewPlayerSnapshotToClientMessage
// extrapolate from B into t seconds into the future, t can be negative
// this is used for generating synthetic snapshot messages in case we detected missing packets
PlayerSnapshotMessage Derhass32b::ExtrapolatePlayerSnapshotMessage(const PlayerSnapshotMessage& B, float t)
{
	PlayerSnapshotMessage C = B;
	size_t j;

	for (j=0; j<B.snapshot.size(); j++) {
		ExtrapolatePlayerSnapshot(C.snapshot[j], B.snapshot[j], t);
    	}
	
	C.message_timestamp = B.message_timestamp + t;
	return C;
}

// Prepare for a new match
// resets all history data and metadata we keep
void Derhass32b::ResetForNewMatch()
{
	ClearRing();
	m_last_update_time = ip->GetGameState().lastTimestamp;
	m_last_frame_time = ip->GetGameState().lastTimestamp;

	m_compensation_sum = 0.0f;
	m_compensation_count = 0;
	m_compensation_interpol_count = 0;
	m_missing_packets_count = 0;
	m_duplicated_packets_count = 0;
	m_compensation_last = ip->GetGameState().lastTimestamp;
}

// add a AddNewPlayerSnapshot(NewPlayerSnapshotToClientMessage
// this should be called as soon as possible after the message arrives
// This function adds the message into the ring buffer, and
// also implements the time sync algorithm between the message sequence and
// the local render time.
void Derhass32b::AddNewPlayerSnapshot(const PlayerSnapshotMessage& msg, SnapshotVersion version)
{
	const GameState& gs=ip->GetGameState();
	if  (m_last_messages_ring_count == 0) {
		// first packet
		EnqueueToRing(msg, false);
		m_last_update_time = gs.lastTimestamp;
		m_unsynced_messages_count = 0;
	} else {
		bool estimateVelocities = (version == SNAPSHOT_VANILLA);
		int deltaFrames;
		if (version != SNAPSHOT_VELOCITY_TIMESTAMP) {
			// we do not have server timestamps
			deltaFrames = 1;
		} else {
			// determine how many frames ahead this new message is relative to
			// the last one we have seen
			deltaFrames = (int)((msg.message_timestamp - m_last_message_server_time) / fixedDeltaTime + 0.5f);
		}

		if (deltaFrames == 1) {
			// FAST PATH
			// next in sequence, as we expected
			EnqueueToRing(msg, estimateVelocities);
			m_unsynced_messages_count++;
		} else if (deltaFrames > 1) {
			// SLOW PATH:
			// we actually do the creation of missing packets here
			// one per received new snapshot, so that the per-frame
			// update code path can stay simple
			const PlayerSnapshotMessage& lastMsg = m_last_messages_ring[m_last_messages_ring_pos_last];
			if (deltaFrames == 2) {
				// there is one missing snapshot
				EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.5f), false);
				EnqueueToRing(msg, false);
			} else if (deltaFrames == 3) {
				// there are two missing snapshots
				EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.3333f),false);
				EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.6667f),false);
				EnqueueToRing(msg, false);
			} else if (deltaFrames ==  4) {
				// there are three missing snapshots
				EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.25f), false);
				EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.5f),  false);
				EnqueueToRing(InterpolatePlayerSnapshotMessage(lastMsg,msg,0.75f), false);
				EnqueueToRing(msg, false);
			} else {
				// there are more than 3 missing snapshots,
				// just take the completely new data in
				EnqueueToRing(ExtrapolatePlayerSnapshotMessage(msg, -3.0f * fixedDeltaTime), false);
				EnqueueToRing(ExtrapolatePlayerSnapshotMessage(msg, -2.0f * fixedDeltaTime), false);
				EnqueueToRing(ExtrapolatePlayerSnapshotMessage(msg, -1.0f * fixedDeltaTime), false);
				EnqueueToRing(msg, false);
			}
			log.Log(Logger::WARN, "detected %d missing snapshots",  deltaFrames - 1);
			m_unsynced_messages_count += deltaFrames;
			m_missing_packets_count += (deltaFrames - 1);
			instrp[INSTR_SKIP_DETECTED].count++;
			instrp[INSTR_SKIPPED_FRAMES].count+=(deltaFrames - 1);
                } else if (deltaFrames < -180) {
			// the message is more than 3 seconds old!
			// This should never happen, and if it does, something really weird
			// is going on, so we better completely re-sync with the server,
			// better safe than sorry!
			ClearRing();
			EnqueueToRing(msg, false);
			m_last_update_time = gs.lastTimestamp;
			m_unsynced_messages_count = 0;
			instrp[INSTR_UNPLAUSIBE_TIMESTAMP].count++;
			log.Log(Logger::WARN, "unplausible packet from the past: %d frames behind, FULL RESYNC", -deltaFrames);
		} else {
			log.Log(Logger::WARN, "ignoring duplicated packet from the past: %d frames behind", -deltaFrames);
			instrp[INSTR_DUPED_FRAMES].count++;
			m_duplicated_packets_count++;
		}
	}
	m_last_message_time = gs.lastTimestamp;
	m_last_message_server_time = msg.message_timestamp;
	//ApplyTimeSync();
}

void Derhass32b::ApplyTimeSync()
{
	if (m_unsynced_messages_count < 1) {
		return;
	}

	const GameState& gs=ip->GetGameState();
	rpcAux[AUX_BUFFER_UPDATE]->Add(gs.lastTimestamp);
	rpcAux[AUX_BUFFER_UPDATE]->Add(gs.lastRealTimestamp);
	rpcAux[AUX_BUFFER_UPDATE]->Add(m_last_update_time);
	m_last_update_time += m_unsynced_messages_count * fixedDeltaTime;
	m_unsynced_messages_count = 0;

	// check if the time base is still plausible
	float delta = (m_last_message_time - m_last_update_time) / fixedDeltaTime; // in ticks
	// allow a sliding window to catch up for latency jitter
	float frameSoftSyncLimit = 2.0f; ///hard-sync if we're off by more than that many physics ticks
	rpcAux[AUX_BUFFER_UPDATE]->Add(m_last_update_time);
	rpcAux[AUX_BUFFER_UPDATE]->Add(delta);
	if (delta < -frameSoftSyncLimit || delta > frameSoftSyncLimit) {
		// hard resync
		float correction = (delta < 0.0f)?std::ceil(delta):std::floor(delta);
		log.Log(Logger::WARN, "hard resync by %f frames, corr: %f", delta, correction);
		m_last_update_time = m_last_message_time; //gs.lastTimestamp;
		//m_last_update_time += correction * fixedDeltaTime;
		instrp[INSTR_HARD_SYNC].count++;
	} else {
		// soft resync
		float smoothing_factor = 0.1f;
		m_last_update_time += smoothing_factor * delta * fixedDeltaTime;
		instrp[INSTR_SOFT_SYNC].count++;
	}
	rpcAux[AUX_BUFFER_UPDATE]->Add(m_last_update_time);
	rpcAux[AUX_BUFFER_UPDATE]->FlushCurrent();
}

void Derhass32b::DoBufferEnqueue(const PlayerSnapshotMessage& msg, const EnqueueInfo& enqueueInfo)
{
	SimulatorBase::DoBufferEnqueue(msg, enqueueInfo);
	SnapshotVersion version = (assumeVersion < 0) ? enqueueInfo.snapshotVersion : (SnapshotVersion)assumeVersion;
	AddNewPlayerSnapshot(msg, version);
}

// Called per frame, moves ships along their interpolation/extrapolation motions
bool Derhass32b::DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{
	SimulatorBase::DoInterpolation(interpolationInfo, results);
	float now = interpolationInfo.timestamp; // needs to be the same time source we use for m_last_update_time
	PlayerSnapshotMessage* msgA = NULL; // interpolation: start
	PlayerSnapshotMessage* msgB = NULL; // interpolation: end, extrapolation start
	float interpolate_factor = 0.0f;			  // interpolation: factor in [0,1]
	float delta_t = 0.0f;
	int interpolate_ticks = 0;
	bool do_interpolation = false;

	// find out which case we have, and get the relevant snapshot message(s)
	/*
	for (int xxx=0; xxx<m_last_messages_ring_count; xxx++) {
		Debug.LogFormat("having snapshot from {0} represents {1}", m_last_messages_ring[(m_last_messages_ring_pos_last + 4 - xxx)&3].m_timestamp, m_last_update_time - xxx* Time.fixedDeltaTime);
	}
	*/
	if (m_last_messages_ring_count < 1) {
		// we do not have any snapshot messages...
		return false;
	}

	ApplyTimeSync();
	delta_t = now + GetShipExtrapolationTime() - m_last_update_time;
	// if we want interpolation, add this as a _negative) offset
	// we use delta_t=0  as the base for from which we extrapolate into the future
	delta_t -= mms_ship_lag_added / 1000.0f;
	// time difference in physics ticks
	float delta_ticks = delta_t / fixedDeltaTime;
	// the number of frames we need to interpolate into
	// <= 0 means no interpolation at all,
	// 1 would mean we use the second most recent and the most recent snapshot, and so on...
	interpolate_ticks = -(int)std::floor(delta_ticks);
	// do we need to do interpolation?
	do_interpolation = (interpolate_ticks > 0);

	if (do_interpolation) {
		// we need interpolate_ticks + 1 elements in the ring buffer
		// NOTE: in the code below, the index [(m_last_messages_ring_pos_last + 4 - i) &3]
		//	   effectively acceses the i-ith most recent element (i starting by 0)
		//	   since 4-(i-1) == 4-i+ 1 = 5-i, 5-i references the next older one
		if ( interpolate_ticks < m_last_messages_ring_count ) {
			msgA = &m_last_messages_ring[(m_last_messages_ring_pos_last + 4 - interpolate_ticks) & 3];
			msgB = &m_last_messages_ring[(m_last_messages_ring_pos_last + 5 - interpolate_ticks) & 3];
			interpolate_factor = delta_ticks - std::floor(delta_ticks);
			instrp[INSTR_INTERPOLATE].count++;
			instrp[INSTR_INTERPOLATE_23 + 1 - interpolate_ticks].count++;
		} else {
			// not enough packets received so far
			// "extrapolate" into the past
			do_interpolation = false;
			// get the oldest snapshot we have
			msgB = &m_last_messages_ring[(m_last_messages_ring_pos_last + 5 - m_last_messages_ring_count) & 3];
			// offset the time for the extrapolation
			// delta_t is currently relative to the most recent element we have,
			// but we need it relative to msgA
			delta_t += fixedDeltaTime * (m_last_messages_ring_count-1);
			instrp[INSTR_EXTRAPOLATE_PAST].count++;
		}
	} else {
		// extrapolation case
		// use the most recently received snapshot
		msgB = &m_last_messages_ring[m_last_messages_ring_pos_last];
		instrp[INSTR_EXTRAPOLATE].count++;
	}
	m_last_frame_time = now;

	/*
	Debug.LogFormat("At: {0} Setting: {1} IntFrames: {2}, dt: {3}, IntFact {4}",now,Menus.mms_ship_max_interpolate_frames, interpolate_ticks, delta_t, interpolate_factor);
	if (interpolate_ticks > 0) {
		Debug.LogFormat("Using A from {0}", msgA.m_timestamp);
		Debug.LogFormat("Using B from {0}", msgB.m_timestamp);
	} else {
		Debug.LogFormat("Using B from {0}", msgB.m_timestamp);
	}
	*/

	// keep statistics
	m_compensation_sum += delta_t;
	m_compensation_count++;
	// NOTE: one can't replace(interpolate_ticks > 0) by do_interpolation here,
	//	   because even in the (interpolate_ticks > 0) case the code above could
	//	   have reset do_interpolation to false because we technically want
	//	   the "extrapolation" into the past thing, but we don't want to count that
	//	   as extrapolation...
	m_compensation_interpol_count += (interpolate_ticks > 0)?1:0;
	if (interpolationInfo.timestamp >= m_compensation_last + 5.0 && m_compensation_count > 0) {
		log.Log(Logger::INFO, "ship lag compensation over last %u frames: %fms / %f physics ticks, %u interpolation (%f%%) packets: %d missing / %d duplicates",
						m_compensation_count, 1000.0f* (m_compensation_sum/ m_compensation_count),
						(m_compensation_sum/m_compensation_count)/fixedDeltaTime,
						m_compensation_interpol_count,
						100.0f*((float)m_compensation_interpol_count/(float)m_compensation_count),
						m_missing_packets_count,
						m_duplicated_packets_count);
		m_compensation_sum = 0.0f;
		m_compensation_count = 0;
		m_compensation_interpol_count = 0;
		m_missing_packets_count = 0;
		m_duplicated_packets_count = 0;
		m_compensation_last = interpolationInfo.timestamp;
	}

	// actually apply the operation to each player
	for (size_t i=0; i<gameState.playerCnt; i++) {
		const Player& cp=gameState.player[i];
		PlayerSnapshot& p=results.player[results.playerCnt];
		p.id = cp.id;
		p.state.timestamp = interpolationInfo.timestamp;
		p.state.realTimestamp = interpolationInfo.realTimestamp;
		if (do_interpolation) {
			PlayerSnapshot *A = GetPlayerSnapshot(p.id, msgA);
			PlayerSnapshot *B = GetPlayerSnapshot(p.id, msgB);
			if (A && B) {
				interpolatePlayer(*A, *B, p, interpolate_factor);
				results.playerCnt++;
			}
		} else {
			PlayerSnapshot *snapshot = GetPlayerSnapshot(p.id, msgB);
			if (snapshot) {
				extrapolatePlayer(*snapshot, p, delta_t);
				results.playerCnt++;
			}
		}
	}

	return true;
}

void Derhass32b::interpolatePlayer(const PlayerSnapshot& A, const PlayerSnapshot& B, PlayerSnapshot& result, float t)
{
	result.id = A.id;

	lerp(A.state.pos, B.state.pos, result.state.pos, t);
	lerp(A.state.vel, B.state.vel, result.state.vel, t);
	slerp(A.state.rot, B.state.rot, result.state.rot, t);
}

void Derhass32b::Start()
{
	size_t i;

	SimulatorBase::Start();
	ClearInstrumentationPoints(instrp, INSTR_COUNT);
	for (i=0; i<AUX_CHANNELS_COUNT; i++) {
		bool isNew;
		rpcAux[i]=resultProcessor.GetAuxChannel(0, registerID, i, isNew);
		if (isNew) {
			rpcAux[i]->SetLogger(&log);
			rpcAux[i]->SetName(fullName.c_str());
			rpcAux[i]->StartStream(ip->GetOutputDir());
			log.Log(Logger::INFO,"created new aux result process channel '%s'", rpcAux[i]->GetName());
		}
	}

	m_last_update_time = 0.0f;
	m_last_frame_time = 0.0f;

	ResetForNewMatch();
}

void Derhass32b::Finish()
{
	size_t i;
	DumpInstrumentationPoints(instrp, INSTR_COUNT);

	for (i=0; i<AUX_CHANNELS_COUNT; i++) {
		rpcAux[i]=NULL;
	}
	SimulatorBase::Finish();
}

} // namespace Simulator;
} // namespace OlmodPlayerDumpState 
