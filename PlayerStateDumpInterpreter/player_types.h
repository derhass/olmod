#ifndef OLMD_PLAYER_TYPES_H
#define OLMD_PLAYER_TYPES_H

#include "math_helper.h"

#include <map>
#include <vector>
#include <cstdint>

namespace OlmodPlayerDumpState {

struct PlayerState {
	float pos[3];
	CQuaternion rot;
	float timestamp; // not transmitted
	
	// in new protocol for 0.3.6RC3
	float vel[3];
	float vrot[3]; // euler angles;
	float message_timestamp; // not directly transmitted, but in the parent message
	float realTimestamp; // not transmitted
	// in new protocol V5, only for transfrom dumps
	bool isTransformDump;
	int32_t  tick;
	int32_t last_ack_tick;

	void Invalidate()
	{
		pos[0] = 0.0f;
		pos[1] = 0.0f;
		pos[2] = 0.0f;

		rot.v[0] = 0.0f;
		rot.v[1] = 0.0f;
		rot.v[2] = 0.0f;
		rot.v[3] = 1.0f;

		timestamp = -1.0f;

		vel[0]=0.0f;
		vel[1]=0.0f;
		vel[2]=0.0f;

		vrot[0]=0.0f;
		vrot[1]=0.0f;
		vrot[2]=0.0f;

		realTimestamp = -1.0f;
		message_timestamp = -1.0f;

		isTransformDump = false;
	}
};

struct PlayerSnapshot {
	uint32_t id;
	PlayerState state;

	void Copy(uint32_t pId, const PlayerState& pState) {
		id = pId;
		state = pState;
	}
};

struct PlayerSnapshotMessage {
	float message_timestamp;
	float recv_timestamp;
	std::vector<PlayerSnapshot> snapshot;
};

struct Player {
	uint32_t id;
	float firstSeen;
	float lastSeen;
	uint32_t waitForRespawn;
	uint32_t waitForRespawnReset;
	PlayerState origState;
	PlayerState mostRecentState;
	void *data;
	
	Player() {
		Invalidate();
	}

	void Invalidate()
	{
		data=NULL;
		id = (uint32_t)-1;
		firstSeen = lastSeen = -1.0f;
		waitForRespawn=0;
		waitForRespawnReset=0;
		origState.Invalidate();
		mostRecentState.Invalidate();
	}
};

typedef std::map<uint32_t, Player> PlayerMap;

} // namespace OlmodPlayerDumpState 


#endif // !OLMD_PLAYER_TYPES_H
