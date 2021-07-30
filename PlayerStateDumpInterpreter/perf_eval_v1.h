#ifndef OLMD_PERF_EVAL_V1_H
#define OLMD_PERF_EVAL_V1_H

#include "perf_eval_base.h"

namespace OlmodPlayerDumpState {
namespace PerfEvaluator {

class V1 : public PerfEvalBase {

	private:

	protected:

		virtual const char *GetBaseName() const;

		virtual void Start();
		virtual void Finish();

		virtual void ProcessPerfProbe(const PerfProbe& probe, bool small);
	public:
		V1(ResultProcessor& rp);
		virtual ~V1();
};


} // namespace PerfEvaluator;
} // namespace OlmodPlayerDumpState


#endif // !OLMD_PERF_EVAL_V1_H
