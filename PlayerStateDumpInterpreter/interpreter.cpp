#include "interpreter.h"
#include "math_helper.h"

#include <sstream>

namespace OlmodPlayerDumpState {

GameState::GameState() :
	playersCycle(0),
	m_InterpolationStartTime(-1.0f),
	ping(0)
{}

Player& GameState::GetPlayer(uint32_t id)
{
	PlayerMap::iterator it = players.find(id);
	if (it == players.end()) {
		playersCycle++;
		Player p;
		p.id = id;
		players[id] = p;
		return players[id];
	}
	return (it->second);
}

const Player* GameState::FindPlayer(uint32_t id) const
{
	PlayerMap::const_iterator it = players.find(id);
	if (it == players.end()) {
		return NULL;
	}
	return &(it->second);
}

Interpreter::Interpreter(ResultProcessor& rp, const char *outputPath) :
	resultProcessor(rp),
	file(NULL),
	fileName(NULL),
	outputDir(outputPath),
	process(false)
{
}

Interpreter::~Interpreter()
{
	CloseFile();
}

void Interpreter::AddSimulator(SimulatorBase& simulator)
{
	unsigned int id = (unsigned)simulators.size() + 100;
	simulators.push_back(&simulator);
	simulator.ip=this;
	simulator.registerID = id;
	simulator.UpdateName();
	log.Log(Logger::DEBUG, "added simulator '%s'", simulator.GetName());
}

void Interpreter::DropSimulators()
{
	log.Log(Logger::DEBUG, "dropping simulator");
	simulators.clear();
}

bool Interpreter::OpenFile(const char *filename)
{
	CloseFile();

	if (!filename) {
		log.Log(Logger::ERROR, "open: no file given");
		return false;
	}

	file = std::fopen(filename, "rb");
	if (!file) {
		log.Log(Logger::ERROR, "open: failed to open '%s'",filename);
		fileName = NULL;
		return false;
	}
	fileName = filename;
	log.Log(Logger::DEBUG, "open: opened '%s'",filename);

	uint32_t version = ReadUint();
	uint32_t hdrExtraSize = ReadUint();
	if (!file) {
		log.Log(Logger::ERROR, "open: failed to read header of '%s'",fileName);
		return false;
	}
	log.Log(Logger::DEBUG, "version: %u",(unsigned)version);

	if (version != 1) {
		log.Log(Logger::ERROR, "open: version of '%s' not supported: %u",fileName,(unsigned)version);
		return false;
	}
	if (hdrExtraSize) {
		if (!std::fseek(file, (long)hdrExtraSize, SEEK_CUR)) {
			log.Log(Logger::ERROR, "open: ifailed to skip extra header of '%s'",fileName);
			CloseFile();
			return false;
		}
	}

	log.Log(Logger::DEBUG, "open: successfully opened '%s'",fileName);
	return true;
}

int32_t Interpreter::ReadInt()
{
	int32_t value = 0;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			log.Log(Logger::WARN, "failed to read int");
			value = 0;
			CloseFile();
		}
	}
	return value;
}

uint32_t Interpreter::ReadUint()
{
	uint32_t value = 0;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			log.Log(Logger::WARN, "failed to read uint");
			value = 0;
			CloseFile();
		}
	}
	return value;
}

float Interpreter::ReadFloat()
{
	float value = 0.0f;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			log.Log(Logger::WARN, "failed to read float");
			value = 0.0f;
			CloseFile();
		}
	}
	return value;
}

void Interpreter::ReadPlayerSnapshot(PlayerSnapshot& s)
{
	s.id = ReadUint();
	s.state.pos[0] = ReadFloat();
	s.state.pos[1] = ReadFloat();
	s.state.pos[2] = ReadFloat();
	s.state.rot.v[0] = ReadFloat();
	s.state.rot.v[1] = ReadFloat();
	s.state.rot.v[2] = ReadFloat();
	s.state.rot.v[3] = ReadFloat();
	if (!file) {
		log.Log(Logger::WARN, "failed to read PlayerSnapshot");
	}
}

uint32_t Interpreter::ReadPlayerSnapshotMessage(PlayerSnapshotMessage& s)
{
	uint32_t i, num = ReadUint();
	s.snapshot.resize(num);
	for (i=0; i<num; i++) {
		ReadPlayerSnapshot(s.snapshot[i]);
	}

	return num;
}


void Interpreter::SimulateBufferEnqueue()
{
	log.Log(Logger::INFO, "ENQUEUE: for %u players", (unsigned)currentSnapshots.snapshot.size());
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->SyncGamestate(gameState);
		sim->DoBufferEnqueue(currentSnapshots);
	}
}

void Interpreter::SimulateBufferUpdate()
{
	log.Log(Logger::INFO,"UPDATE");
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->SyncGamestate(gameState);
		sim->DoBufferUpdate(update);
	}
}

void Interpreter::SimulateInterpolation()
{
	log.Log(Logger::INFO, "INTERPOLATE");
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->SyncGamestate(gameState);
		sim->UpdateWaitForRespawn(gameState);
		InterpolationResults results;
		results.playerCnt = 0;
		bool res = sim->DoInterpolation(interpolation, results);
		if (res && results.playerCnt) {
			sim->ProcessResults(interpolation, results);
		}
	}
	size_t i;
	for (i=0;i<interpolation.lerps.size();i++) {
		const LerpCycle& l=interpolation.lerps[i];
		Player& p=gameState.GetPlayer(l.A.id);
		p.waitForRespawn = l.waitForRespawn_after;
		p.waitForRespawnReset = 0;
	}
}

void Interpreter::ProcessEnqueue()
{
	float ts = ReadFloat();
	uint32_t i, num = ReadPlayerSnapshotMessage(currentSnapshots);
	log.Log(Logger::DEBUG, "got ENQUEUE at %fs for %u players", ts, (unsigned)num);
	for (i=0; i<num; i++) {
		currentSnapshots.snapshot[i].state.timestamp = ts;
		if (file) {
			Player& p = gameState.GetPlayer(currentSnapshots.snapshot[i].id);
			p.mostRecentState = currentSnapshots.snapshot[i].state;
			if (p.firstSeen < 0.0f) {
				p.firstSeen = ts;
			}
			p.lastSeen = ts;
			bool isNew;
			ResultProcessorChannel *rpc = resultProcessor.GetChannel(p.id, 1, isNew);
			if (isNew) {
				rpc->SetLogger(&log);
				rpc->SetName("raw_buffers");
				rpc->StartStream(GetOutputDir());
				log.Log(Logger::INFO,"created new result process channel '%s'", rpc->GetName());
			}
			rpc->Add(currentSnapshots.snapshot[i]);
		}
	}
	log.Log(Logger::DEBUG_DETAIL, currentSnapshots);
	SimulateBufferEnqueue();
}

void Interpreter::ProcessUpdateBegin() 
{
	update.valid = true;
	update.timestamp =  ReadFloat();
	update.before.timestamp = -1.0f;
	update.after.timestamp = -1.0f;
	update.m_InterpolationStartTime_before = ReadFloat();
	log.Log(Logger::DEBUG, "got UPDATE_BEGIN at %fs interpolStart: %fs", update.timestamp, update.m_InterpolationStartTime_before);
}

void Interpreter::ProcessUpdateEnd()
{
	update.m_InterpolationStartTime_after = ReadFloat();

	log.Log(Logger::DEBUG, "got UPDATE_END interpolStart: %fs", update.m_InterpolationStartTime_after);
	if (update.valid) {
		SimulateBufferUpdate();
	}
	update.valid = false;
}

void Interpreter::ProcessInterpolateBegin()
{
	interpolation.valid=true;
	interpolation.timestamp = ReadFloat();
	interpolation.ping = ReadInt();
	interpolation.interpol.timestamp=-1.0f;
	gameState.ping = interpolation.ping;
	log.Log(Logger::DEBUG, "got INTERPOLATE_BEGIN at %fs ping %d", interpolation.timestamp, interpolation.ping);
	interpolation.lerps.clear();
}

void Interpreter::ProcessInterpolateEnd()
{
	log.Log(Logger::DEBUG, "got INTERPOLATE_END");
	if (interpolation.valid) {
		SimulateInterpolation();
	}
	interpolation.valid=false;
}

void Interpreter::ProcessLerpBegin()
{
	LerpCycle c;
	c.waitForRespawn_before = ReadUint();
	ReadPlayerSnapshot(c.A);
	ReadPlayerSnapshot(c.B);
	c.A.state.timestamp = -1.0f; /// we do not know
	c.B.state.timestamp = -1.0f; /// we do not know
	c.t=ReadFloat();
	log.Log(Logger::DEBUG, "got LERP_BEGIN for player %u waitForRespwan=%u t=%f",c.A.id,c.waitForRespawn_before,c.t);
	log.Log(Logger::DEBUG_DETAIL, c.A);
	log.Log(Logger::DEBUG_DETAIL, c.B);
	interpolation.lerps.push_back(c);
	Player &p=gameState.GetPlayer(c.A.id);
	if (!p.waitForRespawn && c.waitForRespawn_before) {
		// was enabled outside of the stuff we were inspecting...
		log.Log(Logger::DEBUG,"player %u: waitForRespawn war reset to 1",(unsigned)p.id);
		p.waitForRespawnReset = 1;
	} else {
		p.waitForRespawnReset = 0;
	}
	p.waitForRespawn=c.waitForRespawn_before;
}

void Interpreter::ProcessLerpEnd()
{
	uint32_t waitForRespawn = ReadUint();
	if (interpolation.lerps.size() < 1) {
		log.Log(Logger::WARN, "LERP_END without lerp begin???");
		return;
	}
	LerpCycle &c = interpolation.lerps[interpolation.lerps.size()-1];
	c.waitForRespawn_after = waitForRespawn;
	log.Log(Logger::DEBUG, "got LERP_END for player %u waitForRespwan=%u",c.A.id,c.waitForRespawn_after);
	Player &p=gameState.GetPlayer(c.A.id);
	// process the original interpolation
	lerp(c.A.state.pos,c.B.state.pos,p.origState.pos,c.t);
	slerp(c.A.state.rot,c.B.state.rot,p.origState.rot,c.t);
	p.origState.timestamp = interpolation.timestamp;

	bool isNew;
	ResultProcessorChannel *rpc = resultProcessor.GetChannel(p.id, 0, isNew);
	if (isNew) {
		rpc->SetLogger(&log);
		rpc->SetName("original_interpolation");
		rpc->StartStream(GetOutputDir());
		log.Log(Logger::INFO,"created new result process channel '%s'", rpc->GetName());
	}
	rpc->Add(p.origState);
	rpc = resultProcessor.GetChannel(p.id, 2, isNew);
	if (isNew) {
		rpc->SetLogger(&log);
		rpc->SetName("raw_most_recent");
		rpc->StartStream(GetOutputDir());
		log.Log(Logger::INFO,"created new result process channel '%s'", rpc->GetName());
	}
	rpc->Add(p.mostRecentState);
}

void Interpreter::ProcessUpdateBufferContents()
{
	float ts = ReadFloat();
	int32_t size = ReadInt();
	uint32_t before = ReadUint();
	uint32_t i, num;

	log.Log(Logger::DEBUG, "got UPDATE BUFFER CONTENTS at %fs size: %d before: %u", ts, (int)size, (unsigned)before);
	UpdateBufferContents *up;
	switch (before) {
		case 0:
			up = &update.after;
			break;
		case 1:
			up = &update.before;
			break;
		default:
			up = &interpolation.interpol;
	}
	UpdateBufferContents& u=*up;

	u.timestamp = ts;
	u.size = size;
	u.before = before;

       	num = ReadPlayerSnapshotMessage(u.A);
	for (i=0; i<num; i++) {
		u.A.snapshot[i].state.timestamp = ts;
	}
       	num = ReadPlayerSnapshotMessage(u.B);
	for (i=0; i<num; i++) {
		u.B.snapshot[i].state.timestamp = ts;
	}
       	num = ReadPlayerSnapshotMessage(u.C);
	for (i=0; i<num; i++) {
		u.C.snapshot[i].state.timestamp = ts;
	}

	log.Log(Logger::DEBUG_DETAIL, u.A);
	log.Log(Logger::DEBUG_DETAIL, u.B);
	log.Log(Logger::DEBUG_DETAIL, u.C);
}

void Interpreter::ProcessLerpParam()
{
	float param = ReadFloat();
	log.Log(Logger::DEBUG, "got LERP PARAMETER %f", param);
}

bool Interpreter::ProcessCommand()
{
	if (!file || feof(file) || ferror(file)) {
		return false;
	}

	Command cmd = (Command)ReadUint();
	log.Log(Logger::DEBUG, "got command 0x%x", (unsigned)cmd);
	switch(cmd) {
		case ENQUEUE:
			ProcessEnqueue();
			break;
		case UPDATE_BEGIN:
			ProcessUpdateBegin();
			break;
		case UPDATE_END:
			ProcessUpdateEnd();
			break;
		case INTERPOLATE_BEGIN:
			ProcessInterpolateBegin();
			break;
		case INTERPOLATE_END:
			ProcessInterpolateEnd();
			break;
		case LERP_BEGIN:
			ProcessLerpBegin();
			break;
		case LERP_END:
			ProcessLerpEnd();
			break;
		case FINISH:
			log.Log(Logger::DEBUG, "got FINISH");
			process = false;
			return true;
		case UPDATE_BUFFER_CONTENTS:
			ProcessUpdateBufferContents();
			break;
		case LERP_PARAM:
			ProcessLerpParam();
			break;
		case INTERPOLATE_PATH_01:
			log.Log(Logger::DEBUG, "got INTERPOLATE_PATH_01");
			break;
		case INTERPOLATE_PATH_12:
			log.Log(Logger::DEBUG, "got INTERPOLATE_PATH_12");
			break;
		default:
			if (file) {
				log.Log(Logger::ERROR, "INVALID COMMAND 0x%x", (unsigned)cmd);
			}
			CloseFile(); 
	}

	return (file != NULL);
}

void Interpreter::CloseFile()
{
	if (file) {
		if (process) {
			log.Log(Logger::WARN, "read error or premature end of file");
		}
		std::fclose(file);
		file = NULL;
		log.Log(Logger::INFO, "closed '%f'", fileName);
	}
	fileName = NULL;
}

bool Interpreter::ProcessFile(const char *filename)
{

	if (!OpenFile(filename)) {
		return false;
	}

	process = true;
	resultProcessor.Clear();

	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->Start();
	}

	// process the file until end or error is reached
	while (process && ProcessCommand());

	CloseFile();

	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->Finish();
	}
	resultProcessor.Finish();

	return true;
}


} // namespace OlmodPlayerDumpState
