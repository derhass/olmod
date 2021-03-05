#include "interpreter.h"
#include "simulator_original.h"
#include "simulator_36rc2.h"
#include "simulator_36rc3.h"
#include "simulator_dh1.h"
#include "simulator_cow1.h"
#include "simulator_dh32.h"
#include "simulator_dh33.h"

#include <clocale>

int main(int argc, char **argv)
{
	OlmodPlayerDumpState::Logger::LogLevel level = OlmodPlayerDumpState::Logger::DEBUG_DETAIL;
	OlmodPlayerDumpState::Logger::LogLevel levelSim  = level;
	const char *dir = (argc>2)?argv[2]:".";

	std::setlocale(LC_ALL,"C");
	OlmodPlayerDumpState::ResultProcessor rp;
	OlmodPlayerDumpState::Interpreter interpreter(rp, dir);

	rp.Configure("dumpDeltaPos=1;");

	interpreter.GetLogger().SetLogFile("interpreter.log",dir);
	interpreter.GetLogger().SetLogLevel(level);
	interpreter.GetLogger().SetStdoutStderr(false);


	OlmodPlayerDumpState::Simulator::Original sOVL(rp);
	interpreter.AddSimulator(sOVL);
	sOVL.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Olmod36RC2 sOlmod36RC2(rp);
	sOlmod36RC2.Configure("max=0;scale=0;ping=0;");
	interpreter.AddSimulator(sOlmod36RC2);
	sOlmod36RC2.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Olmod36RC2 sOlmod36RC2b(rp);
	sOlmod36RC2b.Configure("max=100;scale=100;ping=100;");
	interpreter.AddSimulator(sOlmod36RC2b);
	sOlmod36RC2b.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Olmod36RC3 sOlmod36RC3a(rp);
	sOlmod36RC3a.Configure("max=0;scale=0;ping=0;lag=34;");
	interpreter.AddSimulator(sOlmod36RC3a);
	sOlmod36RC3a.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Olmod36RC3 sOlmod36RC3b(rp);
	sOlmod36RC3b.Configure("max=100;scale=100;ping=100;lag=0;");
	interpreter.AddSimulator(sOlmod36RC3b);
	sOlmod36RC3b.SetLogging(levelSim,  dir);

	/*
	OlmodPlayerDumpState::Simulator::Derhass1 sDH1(rp);
	sDH1.Configure("max=0;scale=0;ping=0;");
	interpreter.AddSimulator(sDH1);
	sDH1.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Derhass1 sDH1b(rp);
	sDH1b.Configure("max=1000;scale=100;ping=133.6;");
	interpreter.AddSimulator(sDH1b);
	sDH1b.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Derhass1 sDH1c(rp);
	sDH1c.Configure("max=1000;scale=100;ping=34;");
	interpreter.AddSimulator(sDH1c);
	sDH1c.SetLogging(levelSim,  dir);
	

	OlmodPlayerDumpState::Simulator::Cow1 sCow1(rp);
	sCow1.Configure("max=0;scale=0;ping=0;");
	interpreter.AddSimulator(sCow1);
	sCow1.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Derhass32 sDH32(rp);
	sDH32.Configure("max=0;scale=0;ping=0;interpol=2;");
	interpreter.AddSimulator(sDH32);
	sDH32.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Derhass32 sDH32b(rp);
	sDH32b.Configure("max=0;scale=0;ping=0;interpol=0;");
	interpreter.AddSimulator(sDH32b);
	sDH32b.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Derhass33 sDH33(rp);
	sDH33.Configure("max=0;scale=0;ping=0;interpol=2;");
	interpreter.AddSimulator(sDH33);
	sDH33.SetLogging(levelSim,  dir);

	OlmodPlayerDumpState::Simulator::Derhass33 sDH33b(rp);
	sDH33b.Configure("max=0;scale=0;ping=0;interpol=0;");
	interpreter.AddSimulator(sDH33b);
	sDH33b.SetLogging(levelSim,  dir);
	*/

	int exit_code = !interpreter.ProcessFile((argc > 1)?argv[1]:"playerstatedump0.olmd");
	return exit_code;
}
