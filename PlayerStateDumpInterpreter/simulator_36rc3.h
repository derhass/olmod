#ifndef OLMD_SIMULATOR_36RC3_H
#define OLMD_SIMULATOR_36RC3_H

#include "simulator_cow1.h"
#include "result_processor.h"

namespace OlmodPlayerDumpState {
namespace Simulator {

class Olmod36RC3 : public Cow1 {
	private:
		typedef enum {
			INSTR_HARD_SYNC=0,
			INSTR_SOFT_SYNC,
			INSTR_INTERPOLATE,
			INSTR_INTERPOLATE_01,
			INSTR_INTERPOLATE_12,
			INSTR_INTERPOLATE_23,
			INSTR_EXTRAPOLATE,
			INSTR_EXTRAPOLATE_PAST,

			INSTR_COUNT
		} InstrumentationPointEnums;

		InstrumentationPoint instrp[INSTR_COUNT];

		typedef enum {
			AUX_BUFFER_UPDATE=0,
			//AUX_INTERPOLATE,

			AUX_CHANNELS_COUNT
		} AuxChannels;

		ResultProcessorAuxChannel* rpcAux[AUX_CHANNELS_COUNT];

	protected:
		float mms_ship_lag_compensation_added_lag;

		// simple statistic
		float m_compensation_sum;
		int m_compensation_count;
		int m_compensation_interpol_count;
		float m_compensation_last;

		// simple ring buffer, use size 4 which is a power of two, so the % 4 becomes simple & 3
		PlayerSnapshotMessage m_last_messages_ring[4];
		int m_last_messages_ring_count;	     // number of elements in the ring buffer
		int m_last_messages_ring_pos_last;   // position of the last added element

		float fixedDeltaTime;

		virtual const char *GetBaseName() const;

		void EnqueueToRing(const PlayerSnapshotMessage& msg);
		void ClearRing();
		void ResetForNewMatch();
		void AddNewPlayerSnapshot(const PlayerSnapshotMessage& msg);

		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);

		virtual void interpolatePlayer(const PlayerSnapshot& A, const PlayerSnapshot& B, PlayerSnapshot& result, float t);

		virtual void Start();
		virtual void Finish();

	public:
		Olmod36RC3(ResultProcessor& rp);
		virtual ~Olmod36RC3();
};


} // namespace Simulator;
} // namespace OlmodPlayerDumpState 


#endif // !OLMD_SIMULATOR_DH32_H
