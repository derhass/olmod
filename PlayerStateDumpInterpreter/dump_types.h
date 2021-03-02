#ifndef OLMD_DUMP_TYPES_H
#define OLMD_DUMP_TYPES_H

#include "player_types.h"

#include <vector>
#include <cstdint>

namespace OlmodPlayerDumpState {

enum Command {
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
	//Version 2
	NEW_ENQUEUE,
	NEW_TIME_SYNC,
	NEW_INTERPOLATE,
	NEW_PLAYER_RESULT,

	// alwways add new commands here
	COMMAND_END_MARKER
};

struct UpdateBufferContents {
	float timestamp;
	int size;
	uint32_t before;
	PlayerSnapshotMessage A;
	PlayerSnapshotMessage B;
	PlayerSnapshotMessage C;
};

struct UpdateCycle {
	bool valid;
	float timestamp;
	float m_InterpolationStartTime_before;
	float m_InterpolationStartTime_after;
	UpdateBufferContents before;
	UpdateBufferContents after;

	UpdateCycle() :
		valid(false)
	{}
};

struct LerpCycle {
	uint32_t waitForRespawn_before;
	uint32_t waitForRespawn_after;
	PlayerSnapshot A,B;
	float t;
};

struct InterpolationCycle {
	bool valid;
	float timestamp;
	int ping;
	std::vector<LerpCycle> lerps;
	UpdateBufferContents interpol;
	// Version 2
	float realTimestamp;
	float unscaledTimestamp;
	float matchTimestamp;
	float timeScale;	


	InterpolationCycle() :
		valid(false)
	{}

	const LerpCycle* FindLerp(uint32_t id) const {
		for (size_t i=0; i<lerps.size(); i++) {
			if (lerps[i].A.id == id) {
				return &lerps[i];
			}
		}
		return NULL;
	}
};

struct NewTimesync {
	float realTimestamp;
	float timestamp;
	float last_update_time;
	float delta;

	void Invalidate() {
		realTimestamp = -1.0f;
		timestamp = -1.0f;
		last_update_time = -1.0f;
		delta = -1.0f;
	}
};

} // namespace OlmodPlayerDumpState 


#endif // !OLMD_DUMP_TYPES_H
