#ifndef OLMD_SIMULATOR_36RC2_H
#define OLMD_SIMULATOR_36RC2_H

#include "simulator_original.h"

namespace OlmodPlayerDumpState {
namespace Simulator {

class Olmod36RC2 : public Original {
	protected:
		float mms_ship_lag_compensation_max;
		float mms_ship_lag_compensation_scale;
		int ping; // set to <= -1000 to use the captured ping

		virtual const char *GetBaseName() const;
		virtual void UpdateName();

		int GetPing();
		float GetShipExtrapolationTime();
		virtual void DoBufferUpdate(const UpdateCycle& updateInfo);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);
		bool LerpRemotePlayer(PlayerSnapshot& p, size_t idx, const InterpolationCycle& interpolationInfo, const PlayerSnapshot&A, const PlayerSnapshot& B, float t);
		float CalculateLerpParameter(float timestamp);

	public:
		Olmod36RC2(ResultProcessor& rp);
		virtual ~Olmod36RC2();
};


} // namespace Simulator;
} // namespace OlmodPlayerDumpState 


#endif // !OLMD_SIMULATOR_36RC2_H
