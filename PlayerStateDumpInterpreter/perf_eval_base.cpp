#include "perf_eval_base.h"
#include "interpreter.h"

namespace OlmodPlayerDumpState {

PerfEvalBase::PerfEvalBase(ResultProcessor& rp) :
	resultProcessor(rp),
	ip(NULL),
	registerID(0)
{
	UpdateName();
}

PerfEvalBase::~PerfEvalBase()
{
}

void PerfEvalBase::Start()
{
	log.Log(Logger::INFO, "start");
	probesCurrent.clear();
	probesPrevious.clear();
}

const char * PerfEvalBase::GetBaseName() const
{
	return "base";
}

const char * PerfEvalBase::GetName() const
{
	return fullName.c_str();
}

void PerfEvalBase::UpdateName()
{
	std::stringstream str;
	str << "perf" << registerID << "_" << GetBaseName();
	if (!nameSuffix.empty()) {
		str << "_" << nameSuffix;
	}
	cfg.GetShortCfg(str,true);
	fullName = str.str();
}

bool PerfEvalBase::SetLogging(Logger::LogLevel l, const char *dir, bool enableStd)
{
	std::stringstream str;

	log.SetLogLevel(l);
	log.SetStdoutStderr(enableStd);
	str << fullName << ".log";
	return log.SetLogFile(str.str().c_str(), dir);
}

void PerfEvalBase::SetSuffix(const char* suffix)
{
	if (suffix) {
		nameSuffix = std::string(suffix);
	} else {
		nameSuffix.clear();
	}
}

bool PerfEvalBase::AddProbe(std::vector<PerfProbe>& probes, const PerfProbe& probe)
{
	bool added = false;
	if (probes.size() <= (size_t)probe.location) {
		size_t i,s = probes.size();
		probes.resize(probe.location+1);
		for (i=s; i<(size_t)probe.location; i++) {
			probes[i].Clear((uint32_t)i, PERF_MODE_BEGIN);
		}
		added = true;
	}
	probes[probe.location] = probe;
	return added;
}

void PerfEvalBase::DoPerfProbe(const PerfProbe& probe)
{
	if (probe.location > 1000) {
		log.Log(Logger::WARN, "probe location %u is unplausible, ignoring it", (unsigned)probe.location);
		return;
	}
	if (probe.mode == (uint32_t)PERF_MODE_BEGIN) {
		if (probesCurrent.size() > (size_t)probe.location) {
			AddProbe(probesPrevious, probesCurrent[probe.location]);
		} else {
			PerfProbe empty;
			empty.Clear(probe.location, probe.mode);
			AddProbe(probesPrevious, empty);
		}
		AddProbe(probesCurrent, probe);
	} else {
		if (probesCurrent.size() <= (size_t)probe.location) {
			log.Log(Logger::WARN, "probe location %u, mode %u has no BEGIN, ignoring it",
				(unsigned)probe.location, (unsigned)probe.mode);
			return;
		}
	}

	ProcessPerfProbe(probe);

	AddProbe(lastInLocation, probe);
}

void PerfEvalBase::ProcessPerfProbe(const PerfProbe& probe)
{
	(void)probe;
}

void PerfEvalBase::Finish()
{
	log.Log(Logger::INFO, "finish");
}

void PerfEvalBase::Configure(const char *options)
{
	if (options) {
		cfg.Parse(options);
		UpdateName();
	}
}

}; // namespace OlmodPlayerDumpState
