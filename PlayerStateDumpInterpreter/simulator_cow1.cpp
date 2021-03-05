#include "simulator_cow1.h"

#include "interpreter.h"
#include "math_helper.h"

#include <sstream>

namespace OlmodPlayerDumpState {
namespace Simulator {

Cow1::Cow1(ResultProcessor& rp) :
	SimulatorBase(rp),
	mms_ship_lag_compensation_max(100.0f),
	mms_ship_lag_compensation_scale(50.0f),
	ping(-1000)
{
	cfg.Add(ConfigParam(mms_ship_lag_compensation_max,"compensation_max", "max"));
	cfg.Add(ConfigParam(mms_ship_lag_compensation_scale,"compensation_scale", "scale"));
	cfg.Add(ConfigParam(ping,"ping"));
}

Cow1::~Cow1()
{
}

const char *Cow1::GetBaseName() const
{
	return "cow1";
}

int Cow1::GetPing()
{
	if (ping <= -1000) {
		return ip->GetGameState().ping;
	}
	return ping;
}

// How far ahead to advance ships, in seconds.
float Cow1::GetShipExtrapolationTime()
{
	float time_ms = (float)GetPing();
	if (time_ms > mms_ship_lag_compensation_max) {
		time_ms = mms_ship_lag_compensation_max;
	}
	return (mms_ship_lag_compensation_scale / 100.0f) * time_ms / 1000.0f;
}

PlayerSnapshot* Cow1::GetPlayerSnapshot(uint32_t playerId, PlayerSnapshotMessage* msg)
{
	if (!msg) {
		return NULL;
	}
	for (size_t i=0; i< msg->snapshot.size(); i++) {
		if (msg->snapshot[i].id == playerId) {
			return &msg->snapshot[i];
		}
	}
	return NULL;
}

void Cow1::DoBufferEnqueue(const PlayerSnapshotMessage& msg)
{
	SimulatorBase::DoBufferEnqueue(msg);

	m_last_update = msg;
	m_last_update_time = ip->GetGameState().lastMatchTimestamp; // TODO: we don't have the correct one...
}

void Cow1::DoBufferUpdate(const UpdateCycle& updateInfo)
{
	// completely bypass this
}

bool Cow1::DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{
	SimulatorBase::DoInterpolation(interpolationInfo, results);
	float now = ip->GetGameState().lastMatchTimestamp;
	float delta_t = now - m_last_update_time;

	delta_t += GetShipExtrapolationTime();

	for (size_t i=0; i<gameState.playerCnt; i++) {
		const Player& cp=gameState.player[i];
		PlayerSnapshot& p=results.player[results.playerCnt];
		p.id=cp.id;
		PlayerSnapshot *snapshot = GetPlayerSnapshot(p.id, &m_last_update);
		if (snapshot) {
			extrapolatePlayer(*snapshot, p, delta_t);
			p.state.timestamp = interpolationInfo.timestamp;
			p.state.realTimestamp = interpolationInfo.realTimestamp;
			results.playerCnt++;
		}
	}
	m_last_frame_time = now;

	return true;
}

void Cow1::extrapolatePlayer(const PlayerSnapshot& A, PlayerSnapshot& result, float t)
{
	result.id = A.id;
	float npos[3];
	npos[0]=A.state.pos[0] + A.state.vel[0];
	npos[1]=A.state.pos[1] + A.state.vel[1];
	npos[2]=A.state.pos[2] + A.state.vel[2];

	lerp(A.state.pos, npos, result.state.pos, t);
	result.state.vel[0] = A.state.vel[0];
	result.state.vel[1] = A.state.vel[1];
	result.state.vel[2] = A.state.vel[2];
	// TODO: rotation
	result.state.rot = A.state.rot;
}

void Cow1::Start()
{
	SimulatorBase::Start();

	m_last_update_time = 0.0f;
	m_last_frame_time = 0.0f;

	m_last_update.snapshot.clear();
}

void Cow1::Finish()
{
	SimulatorBase::Finish();
}

} // namespace Simulator;
} // namespace OlmodPlayerDumpState 
