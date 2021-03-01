#include "logger.h"
#include "player_types.h"

#include <string>
#include <sstream>
#include <cstring>

namespace OlmodPlayerDumpState {

Logger::Logger() :
	file(NULL),
	copyWarnings(NULL),
	copyInfos(NULL),
	level(WARN)
{}

Logger::~Logger()
{
	Stop();
}

bool Logger::Start(const char *filename)
{
	file = std::fopen(filename, "wt");
	return (file != NULL);
}

void Logger::Stop()
{
	if (file) {
		std::fclose(file);
		file=NULL;
	}
}

void Logger::MakeIndent(char *buffer, size_t size, int indent)
{
	size_t num = (indent < 1)?0:(((size_t)indent>=size)?(size-1):(size_t)indent);
	std::memset(buffer, ' ', num);
	buffer[num]=0;
}

bool Logger::SetLogFile(const char *filename, const char *dir)
{

	Stop();
	if (filename) {
		std::stringstream str;
		if (dir) {
			str << dir << '/';
		}
		str << filename;
		return Start(str.str().c_str());
	}
	return true;
}

void Logger::SetLogLevel(LogLevel l)
{
	level = l;
}

void Logger::SetStdoutStderr(bool enabled)
{
	if (enabled) {
		copyInfos = stdout;
		copyWarnings = stderr;
	} else {
		copyInfos = NULL;
		copyWarnings = NULL;
	}
}

void Logger::Log(LogLevel l, const char *fmt, ...)
{
	if (l > level) {
		return;
	}

	std::va_list args;
	if (file) {
		va_start(args, fmt);
		std::vfprintf(file, fmt, args);
		std::fputc('\n', file);
		std::fflush(file);
		va_end(args);
	}
	if (l >= INFO && copyInfos) {
		va_start(args, fmt);
		std::vfprintf(copyInfos, fmt, args);
		std::fputc('\n', copyInfos);
		std::fflush(copyInfos);
		va_end(args);

	} else if (l <= WARN && copyWarnings) {
		va_start(args, fmt);
		std::vfprintf(copyWarnings, fmt, args);
		std::fputc('\n', copyWarnings);
		std::fflush(copyWarnings);
		va_end(args);
	}
}

void Logger::Log(LogLevel l, const PlayerState& s, int indent)
{
	if (l > level) {
		return;
	}

	char buf[32];
	MakeIndent(buf, sizeof(buf), indent);
	Log(l, "%spos (%f %f %f) rot (%f %f %f %f) timestamp %fs",
		buf, s.pos[0], s.pos[1], s.pos[2],
		s.rot.v[0], s.rot.v[1], s.rot.v[2], s.rot.v[3],
		s.timestamp);
}

void Logger::Log(LogLevel l, const PlayerSnapshot& s, int indent)
{
	if (l > level) {
		return;
	}

	char buf[32];
	MakeIndent(buf, sizeof(buf), indent);
	Log(l, "%sPlayerSnapshot %u", buf, (unsigned)s.id);
	Log(l, s.state, indent+2);
}

void Logger::Log(LogLevel l, const PlayerSnapshotMessage& msg, int indent)
{
	if (l > level) {
		return;
	}

	char buf[32];
	MakeIndent(buf, sizeof(buf), indent);
	Log(l, "%sPlayerSnapshotMessage %u players", buf, (unsigned)msg.snapshot.size());
	for (size_t i=0; i<msg.snapshot.size(); i++) {
		Log(l,msg.snapshot[i], indent+2);
	}
}

}; // namespace OlmodPlayerDumpState 
