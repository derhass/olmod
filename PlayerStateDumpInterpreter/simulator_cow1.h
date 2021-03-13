#ifndef OLMD_SIMULATOR_COW1_H
#define OLMD_SIMULATOR_COW1_H

#include "simulator_base.h"
#include "result_processor.h"

namespace OlmodPlayerDumpState {
namespace Simulator {

class Cow1 : public SimulatorBase {
	protected:
		float mms_ship_lag_compensation_max;
		float mms_ship_lag_compensation_scale;
		int ping; // set to <= -1000 to use the captured ping

		float m_last_update_time;
		float m_last_frame_time;
		PlayerSnapshotMessage m_last_update;

		virtual const char *GetBaseName() const;

		PlayerSnapshot* GetPlayerSnapshot(uint32_t playerId, PlayerSnapshotMessage* msg);
		
		int GetPing();
		virtual float GetShipExtrapolationTime();
		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg, const EnqueueInfo& enqueueInfo);
		virtual void DoBufferUpdate(const UpdateCycle& updateInfo);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);

		virtual void extrapolatePlayer(const PlayerSnapshot& snapshot, PlayerSnapshot& result, float t);

		virtual void Start();
		virtual void Finish();

	public:
		Cow1(ResultProcessor& rp);
		virtual ~Cow1();
};


} // namespace Simulator;
} // namespace OlmodPlayerDumpState 


#endif // !OLMD_SIMULATOR_COW1_H
