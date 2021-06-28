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
	// Version 3: Perf probes
	PERF_PROBE,

	// alwways add new commands here
	COMMAND_END_MARKER
};

enum SnapshotVersion {
	SNAPSHOT_VANILLA = 0,
	SNAPSHOT_VELOCITY,
	SNAPSHOT_VELOCITY_TIMESTAMP
};

struct EnqueueInfo {
	float timestamp;
	float realTimestamp;
	float unscaledTimestamp;
	float matchTimestamp;
	float timeScale;
	int wasOld;
	SnapshotVersion snapshotVersion;

	void Clear();
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

enum PerfProbeLocation: uint32_t {
	PERF_LOC_GAMEMANAGER_UPDATE=0,
	PERF_LOC_GAMEMANAGER_FIXED_UPDATE,
};

enum PerfProbleMode: uint32_t {
	PERF_MODE_BEGIN=0,
	PERF_MODE_END,
	PERF_MODE_GENERIC_START,
};

struct PerfProbe {
	uint32_t location;
	uint32_t mode;
	double ts;
	float timeStamp;
	float fixedTimeStamp;
	float realTimeStamp;

	void Clear(uint32_t loc = 0, uint32_t m = 0);
	void Diff(const PerfProbe& a, const PerfProbe& b);
};

} // namespace OlmodPlayerDumpState 


#endif // !OLMD_DUMP_TYPES_H
