#include "interpreter.h"
#include "simulator_original.h"
#include "simulator_36rc2.h"
#include "simulator_36rc3.h"
#include "simulator_dh1.h"
#include "simulator_cow1.h"
#include "simulator_dh32.h"
#include "simulator_dh33.h"

#include <clocale>
#include <sstream>
#include <cstdio>

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

	/*
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
	*/

	int num=100;
	for (int ping=20; ping<110; ping+=20) {
		for (int i=0; i<5; i++) {
			float m=100.0f;
			float s=0.0f;
			float l=0.0f;
			std::stringstream str;
			const char *name;
			if (i == 0) {
				num+=1;
				l=34.0f;
				name="off +34ms lag";
			} else if (i==1) {
				num++;
				l=0.0f;
				name="off +0ms lag";
			} else {
				const char *names[]={
					"weak",
					"medium",
					"strong"};
				s = ((float)(i-1)*100.0f)/3.0f;
				name=names[i-2];
				num++;

			}
			str << "max="<<m<<";scale="<<s<<";ping="<<ping<<";lag="<<l<<";";
			OlmodPlayerDumpState::Simulator::Olmod36RC3 *sim = new OlmodPlayerDumpState::Simulator::Olmod36RC3(rp);
			sim->Configure(str.str().c_str());
			interpreter.AddSimulator(*sim);
			sim->SetLogging(levelSim, dir);

			std::stringstream str2;
			str2 << "max"<<m<<"_scale"<<s<<"_ping"<<ping<<"_lag"<<l;
			const char *ccc=sim->GetFullName();


			std::printf("set output 'ping%d_%s.png'\n",ping,name);
			//std::printf("plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-%f):2 w l lw 2 t 'estimated ground truth on server', 'res_o101_p1138_sim101_olmod-0.3.6-rc3_max100_scale0_ping%d_lag34.csv' u 9:2 w l lw 2 t 'off +34ms lag', 'res_o%d_p1138_sim%d_olmod-0.3.6-rc3_max100_scale%.4f_ping%d_lag0.csv' u 9:2 w l lw 2 t '%s'\n",
			std::printf("plot 'res_o1_p1138_raw_buffers.csv' u ($0/60+199.1635-%f):2 w l lw 2 t 'estimated ground truth on server', 'res_o%d_p1138_%s.csv' u 9:2 w l lw 2 t '0.3.6 ship lag compensation: %s', 'res_o100_p1138_sim100_original.csv' u 9:2 w l lw 1 t 'vanilla overload interpolation' \n",
				ping/1000.0f,num,ccc,name);
			std::printf("\n");
		}
	}

	/*
	OlmodPlayerDumpState::Simulator::Olmod36RC3 sOlmod36RC3b(rp);
	sOlmod36RC3b.Configure("max=100;scale=100;ping=100;lag=0;");
	interpreter.AddSimulator(sOlmod36RC3b);
	sOlmod36RC3b.SetLogging(levelSim,  dir);
	*/

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
