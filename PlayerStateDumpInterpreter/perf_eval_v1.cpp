#include "perf_eval_v1.h"
#include "interpreter.h"

namespace OlmodPlayerDumpState {
namespace PerfEvaluator {

V1::V1(ResultProcessor& rp) :
	PerfEvalBase(rp)
{
}

V1::~V1()
{
}

const char *V1::GetBaseName() const
{
	return "v1";
}

void V1::Start()
{
	PerfEvalBase::Start();
}

void V1::Finish()
{
	PerfEvalBase::Finish();
}

void V1::ProcessPerfProbe(const PerfProbe& probe)
{
	if (probe.mode == (uint32_t)PERF_MODE_END) {
		bool isNew;
		ResultProcessorAuxChannel *rpc = resultProcessor.GetAuxChannel(0, probe.location, 0, isNew);
		if (rpc) {
			if (isNew) {
				rpc->SetLogger(&log);
				rpc->SetName("perf_stats");
				rpc->StartStream(ip->GetOutputDir());
				log.Log(Logger::INFO,"created new result process channel '%s'", rpc->GetName());
			}
			PerfProbe pd;

			rpc->Add(probesCurrent[probe.location]);
			rpc->Add(probe);
			pd.Diff(probesCurrent[probe.location],probesPrevious[probe.location]);
			rpc->Add(pd);
			pd.Diff(probe,probesCurrent[probe.location]);
			rpc->Add(pd);
			rpc->FlushCurrent();
		}
	} else if (probe.mode != (uint32_t)PERF_MODE_BEGIN) {
		if (lastInLocation.size() > (size_t)probe.location && probesCurrent.size() > (size_t)probe.location) {
			bool isNew;
			ResultProcessorAuxChannel *rpc = resultProcessor.GetAuxChannel(0, probe.location, probe.mode, isNew);
			if (rpc) {
				if (isNew) {
					rpc->SetLogger(&log);
					rpc->SetName("perf_stats_delta");
					rpc->StartStream(ip->GetOutputDir());
					log.Log(Logger::INFO,"created new result process channel '%s'", rpc->GetName());
				}
				PerfProbe pd;

				rpc->Add(probe);
				pd.Diff(probe, lastInLocation[probe.location]);
				rpc->Add(pd);
				pd.Diff(probe,probesCurrent[probe.location]);
				rpc->Add(pd);
				rpc->Add(lastInLocation[probe.location].mode);
				rpc->FlushCurrent();

			}
		}
	}
}

} // namespace PerfEvaluator;
} // namespace OlmodPlayerDumpState
