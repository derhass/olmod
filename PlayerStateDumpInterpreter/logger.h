#ifndef OLMD_LOGGER_H
#define OLMD_LOGGER_H

#include <cstdio>
#include <cstdarg>

namespace OlmodPlayerDumpState {

struct PlayerState;
struct PlayerSnapshot;
struct PlayerSnapshotMessage;

class Logger {
	public:
		typedef enum {
			FATAL=0,
			ERROR,
			WARN,
			INFO,
			DEBUG,
			DEBUG_DETAIL
		} LogLevel;

	protected:
		std::FILE *file;
		std::FILE *copyWarnings;
		std::FILE *copyInfos;

		LogLevel level;

		bool Start(const char *filename);
		void Stop();
		void MakeIndent(char* buffer, size_t size, int indent);
		
	public:
		Logger();
		~Logger();

		bool SetLogFile(const char *filename, const char *dir=".");
		void SetLogLevel(LogLevel l);
		void SetStdoutStderr(bool enabled=true);
		void Log(LogLevel l, const char *fmt, ...);
		void Log(LogLevel l, const PlayerState& s, int indent=2);
		void Log(LogLevel l, const PlayerSnapshot& s, int indent=2);
		void Log(LogLevel l, const PlayerSnapshotMessage& msg, int indent=2);
};

}; // namespace OlmodPlayerDumpState 


#endif // !OLMD_LOGGER_H
