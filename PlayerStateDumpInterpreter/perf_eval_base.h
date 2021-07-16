#ifndef OLMD_PERFEVAL_BASE_H
#define OLMD_PERFEVAL_BASE_H

#include "config.h"
#include "dump_types.h"
#include "result_processor.h"

#include <string>
#include <vector>
#include <cstdint>

namespace OlmodPlayerDumpState {

class Interpreter;

class PerfEvalBase {
	private:
		bool AddProbe(std::vector<PerfProbe>& probes, const PerfProbe& probe);

	protected:
		std::vector<PerfProbe> probesCurrent;
		std::vector<PerfProbe> probesPrevious;
		std::vector<PerfProbe> lastInLocation;
		ResultProcessor& resultProcessor;
		const Interpreter* ip;
		Config cfg;

		std::string fullName;
		std::string nameSuffix;
		unsigned int registerID;

		Logger log;

		friend class Interpreter;

		virtual void ProcessPerfProbe(const PerfProbe& probe);

		virtual void Start();
		virtual void DoPerfProbe(const PerfProbe& probe);
		virtual void Finish();


		virtual void UpdateName();
		virtual const char *GetName() const;
		virtual const char *GetBaseName() const;

	public:
		PerfEvalBase(ResultProcessor& rp);
		virtual ~PerfEvalBase();

		Logger& GetLogger() {return log;}
		bool SetLogging(Logger::LogLevel l=Logger::WARN, const char *dir=".", bool enableStd=false);
		void SetSuffix(const char* suffix = NULL);

		void Configure(const char *options);
};

typedef std::vector<PerfEvalBase*> PerfEvalSet;
} // namespace OlmodPlayerDumpState

#endif // !OLMD_PERFEVAL_BASE_H
