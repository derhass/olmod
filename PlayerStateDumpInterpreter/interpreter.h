#ifndef OLMD_INTERPRETER_H
#define OLMD_INTERPRETER_H

#include "config.h"
#include "dump_types.h"
#include "logger.h"
#include "math_helper.h"
#include "player_types.h"
#include "simulator_base.h"
#include "perf_eval_base.h"

#include <string>
#include <map>
#include <vector>
#include <cstdio>
#include <cstdint>

namespace OlmodPlayerDumpState {

const uint32_t NewTimesyncCount = 4;

class ResultProcessor;

struct GameState {
	PlayerMap players;
	uint32_t playersCycle;
	float m_InterpolationStartTime;
	int ping;

	//Version 2
	float timeScale;
	NewTimesync timeSync[NewTimesyncCount];
	NewTimesync timeSyncAfter;
	float lastRealTimestamp;
	float lastTimestamp;
	float lastMatchTimestamp;

	GameState();
	void Reset();
	Player& GetPlayer(uint32_t id);
	const Player* FindPlayer(uint32_t id) const;
};

class Interpreter {
	protected:
		Logger log;
		ResultProcessor& resultProcessor;
		GameState gameState;
		std::FILE *file;
		uint32_t fileVersion;
		const char *fileName;
		const char *outputDir;
		bool process;
		PlayerSnapshotMessage currentSnapshots;
		EnqueueInfo enqueue;
		UpdateCycle update;
		InterpolationCycle interpolation;
		SimulatorSet simulators;
		PerfEvalSet perfEvals;

		bool OpenFile(const char *filename);
		void CloseFile();

		int32_t ReadInt();
		uint32_t ReadUint();
		float ReadFloat();
		double ReadDouble();
		void ReadPlayerSnapshot(PlayerSnapshot& s);
		uint32_t ReadPlayerSnapshotMessage(PlayerSnapshotMessage& s);
		void ReadNewPlayerSnapshot(PlayerSnapshot& s, float ts);
		uint32_t ReadNewPlayerSnapshotMessage(PlayerSnapshotMessage& s);

	        void SimulateBufferEnqueue();
		void SimulateBufferUpdate();
		void SimulateInterpolation();

		void UpdatePlayerAtEnqueue(int idx, float ts);

		void ProcessEnqueue();
		void ProcessNewEnqueue();
		void ProcessUpdateBegin();
		void ProcessUpdateEnd();
		void ProcessInterpolateBegin();
		void ProcessInterpolateEnd();
		void ProcessLerpBegin();
		void ProcessLerpEnd();
		void ProcessUpdateBufferContents();
		void ProcessLerpParam();
		void ProcessNewTimeSync();
		void ProcessNewInterpolate();
		void ProcessNewPlayerResult();
		void ProcessPerfProbe();
		void ProcessPerfProbeSmall();
		void ProcessTransformDump();

		bool ProcessCommand();

	public:
		Interpreter(ResultProcessor& rp, const char *outputPath=".");
		~Interpreter();

		void AddSimulator(SimulatorBase& simulator);
		void DropSimulators();

		void AddPerfEval(PerfEvalBase& perfEval);
		void DropPerfEvals();

		bool ProcessFile(const char *filename);

		Logger& GetLogger() {return log;};
		const GameState& GetGameState() const {return gameState;}
		const char *GetOutputDir() const {return outputDir;}

};	

}; // namespace OlmodPlayerDumpState 


#endif // !OLMD_INTERPRETER_H
