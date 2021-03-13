#include "interpreter.h"
#include "math_helper.h"

#include <sstream>

namespace OlmodPlayerDumpState {

void EnqueueInfo::Clear()
{
	timestamp = -1.0f;
	realTimestamp = -1.0f;
	unscaledTimestamp = -1.0f;
	matchTimestamp = -1.0f;
	timeScale = -1.0f;
	wasOld = 0;
}

GameState::GameState() :
	playersCycle(0)
{
	Reset();
}

void GameState::Reset()
{
	playersCycle++;
	players.clear();

	m_InterpolationStartTime = -1.0f;
	ping = 0;
	timeScale = 1.0;

	for (uint32_t i=0; i<NewTimesyncCount; i++) {
		timeSync[i].Invalidate();
	}
	timeSyncAfter.Invalidate();

	lastRealTimestamp = -1.0f;
	lastTimestamp = -1.0f;
	lastMatchTimestamp = -1.0f;
}

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
	fileVersion(0),
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

	fileVersion = ReadUint();
	uint32_t hdrExtraSize = ReadUint();
	if (!file) {
		log.Log(Logger::ERROR, "open: failed to read header of '%s'",fileName);
		return false;
	}
	log.Log(Logger::DEBUG, "version: %u",(unsigned)fileVersion);

	if (fileVersion < 1 || fileVersion > 4) {
		log.Log(Logger::ERROR, "open: version of '%s' not supported: %u",fileName,(unsigned)fileVersion);
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
	s.state.vel[0] = 0.0f;
	s.state.vel[1] = 0.0f;
	s.state.vel[2] = 0.0f;
	s.state.vrot[0] = 0.0f;
	s.state.vrot[1] = 0.0f;
	s.state.vrot[2] = 0.0f;
	s.state.timestamp = -1.0f;
	s.state.realTimestamp = -1.0f;
	s.state.message_timestamp = -1.0f;
	if (!file) {
		log.Log(Logger::WARN, "failed to read PlayerSnapshot");
	}
}

void Interpreter::ReadNewPlayerSnapshot(PlayerSnapshot& s, float ts)
{
	s.id = ReadUint();
	s.state.pos[0] = ReadFloat();
	s.state.pos[1] = ReadFloat();
	s.state.pos[2] = ReadFloat();
	s.state.rot.v[0] = ReadFloat();
	s.state.rot.v[1] = ReadFloat();
	s.state.rot.v[2] = ReadFloat();
	s.state.rot.v[3] = ReadFloat();
	s.state.vel[0] = ReadFloat();
	s.state.vel[1] = ReadFloat();
	s.state.vel[2] = ReadFloat();
	s.state.vrot[0] = ReadFloat();
	s.state.vrot[1] = ReadFloat();
	s.state.vrot[2] = ReadFloat();
	s.state.timestamp = -1.0f;
	s.state.realTimestamp = -1.0f;
	s.state.message_timestamp = -1.0f;
	if (!file) {
		log.Log(Logger::WARN, "failed to read PlayerSnapshot");
	}
}

uint32_t Interpreter::ReadPlayerSnapshotMessage(PlayerSnapshotMessage& s)
{
	uint32_t i, num = ReadUint();
	s.message_timestamp = -1.0f;
	s.recv_timestamp = -1.0f;
	s.snapshot.resize(num);
	for (i=0; i<num; i++) {
		ReadPlayerSnapshot(s.snapshot[i]);
	}

	return num;
}

uint32_t Interpreter::ReadNewPlayerSnapshotMessage(PlayerSnapshotMessage& s)
{
	uint32_t i;
	s.message_timestamp = ReadFloat();
	s.recv_timestamp = -1.0f;
	uint32_t num = ReadUint();
	s.snapshot.resize(num);
	for (i=0; i<num; i++) {
		ReadNewPlayerSnapshot(s.snapshot[i], s.message_timestamp);
	}

	return num;
}


void Interpreter::SimulateBufferEnqueue()
{
	log.Log(Logger::INFO, "ENQUEUE: for %u players", (unsigned)currentSnapshots.snapshot.size());
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->SyncGamestate(gameState);
		sim->DoBufferEnqueue(currentSnapshots, enqueue);
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

void Interpreter::UpdatePlayerAtEnqueue(int idx, float ts)
{
	if (file) {
		Player& p = gameState.GetPlayer(currentSnapshots.snapshot[idx].id);
		p.mostRecentState = currentSnapshots.snapshot[idx].state;
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
		rpc->Add(currentSnapshots.snapshot[idx]);
	}
}

void Interpreter::ProcessEnqueue()
{
	enqueue.Clear();
	enqueue.timestamp = ReadFloat();
	enqueue.wasOld = 1;
	uint32_t i, num = ReadPlayerSnapshotMessage(currentSnapshots);
	log.Log(Logger::DEBUG, "got ENQUEUE at %fs for %u players", enqueue.timestamp, (unsigned)num);
	for (i=0; i<num; i++) {
		currentSnapshots.snapshot[i].state.timestamp = enqueue.timestamp;
		UpdatePlayerAtEnqueue(i, enqueue.timestamp);
	}
	log.Log(Logger::DEBUG_DETAIL, currentSnapshots);
	gameState.lastTimestamp = enqueue.timestamp;
	SimulateBufferEnqueue();
}

void Interpreter::ProcessNewEnqueue()
{
	enqueue.Clear();
	enqueue.realTimestamp = ReadFloat();
	enqueue.timestamp = ReadFloat();
	if (fileVersion >= 3) {
		enqueue.unscaledTimestamp = ReadFloat();
		enqueue.timeScale=ReadFloat();
		enqueue.matchTimestamp = ReadFloat();
		gameState.lastMatchTimestamp = enqueue.matchTimestamp;
		gameState.timeScale = enqueue.timeScale;
	}
	if (fileVersion >= 4) {
		enqueue.wasOld = ReadInt();
	}
	uint32_t i, num = ReadNewPlayerSnapshotMessage(currentSnapshots);
	log.Log(Logger::DEBUG, "got NEW ENQUEUE at rts%fs ts%fs for %u players, matchts:%f messagets:%f, wasOld:%d", 
		enqueue.realTimestamp, enqueue.timestamp, (unsigned)num, 
		enqueue.matchTimestamp,currentSnapshots.message_timestamp,
		enqueue.wasOld);
	currentSnapshots.recv_timestamp = enqueue.realTimestamp;
	for (i=0; i<num; i++) {
		currentSnapshots.snapshot[i].state.realTimestamp = enqueue.realTimestamp;
		currentSnapshots.snapshot[i].state.timestamp = enqueue.timestamp;
		UpdatePlayerAtEnqueue(i, enqueue.timestamp);
	}
	log.Log(Logger::DEBUG_DETAIL, currentSnapshots);
	gameState.lastTimestamp = enqueue.timestamp;
	gameState.lastRealTimestamp = enqueue.realTimestamp;
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
	gameState.lastTimestamp = update.timestamp;
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

	// we don't have these v2 data
	interpolation.realTimestamp = -1.0f;
	interpolation.unscaledTimestamp = -1.0f;
	interpolation.matchTimestamp = -1.0f;
	interpolation.timeScale = -1.0f;
}

void Interpreter::ProcessInterpolateEnd()
{
	log.Log(Logger::DEBUG, "got INTERPOLATE_END");
	gameState.lastTimestamp = interpolation.timestamp;
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

void Interpreter::ProcessNewTimeSync()
{
	uint32_t place = ReadUint();
	float rts = ReadFloat();
	float ts = ReadFloat();
	float update_time = ReadFloat();
	float delta = ReadFloat();

	log.Log(Logger::DEBUG, "got NEW_TIME_SYNC: Place %u: update_time is now %f, delta %f at rts%f ts%f",
		(unsigned)place, update_time, delta, rts, ts);

	if (place < NewTimesyncCount) {
		NewTimesync &n = gameState.timeSync[place];
		n.realTimestamp = rts;
		n.timestamp = ts;
		n.last_update_time = update_time;
		n.delta = delta;

		if (place > 1) {
			gameState.timeSyncAfter = n;
		}
	}
}

void Interpreter::ProcessNewInterpolate()
{
	float rts = ReadFloat();
	float ts = ReadFloat();
	float uts = ReadFloat();
	float scale = ReadFloat();
	float match = ReadFloat();
	int ping = 0;
	float extrapol = 0.0f;
	if (fileVersion >= 3) {
		ping = ReadInt();
		extrapol = ReadFloat();
	}
	(void)extrapol; // TODO: dump this somewhere, aux channel maybe??? 
	log.Log(Logger::DEBUG, "got NEW_INTERPOLATE rts %f ts %f uts %f match %f timeScale %f ping %d",
		rts,ts,uts,match,scale,ping);

	interpolation.timestamp = ts;
	interpolation.realTimestamp = rts;
	interpolation.unscaledTimestamp = uts;
	interpolation.matchTimestamp = match;
	interpolation.timeScale = scale;

	interpolation.valid = true;
	interpolation.ping = ping;
	gameState.ping = interpolation.ping;
	gameState.timeScale = scale;

	interpolation.valid=true;

	// Fake date for V1: update cycle
	update.valid = true;
	update.timestamp = ts;
	update.before.timestamp = -1.0f;
	update.after.timestamp = -1.0f;
	update.m_InterpolationStartTime_before = -1.0f;
	update.m_InterpolationStartTime_after = -1.0f;
	gameState.lastTimestamp = ts;
	gameState.lastRealTimestamp = rts;
	gameState.lastMatchTimestamp = match;

	if (update.valid) {
		SimulateBufferUpdate();
	}
	update.valid = false;

	// Fake data for V1: LERP cycles
	interpolation.lerps.clear();
	PlayerMap::iterator it;
	for (it = gameState.players.begin(); it != gameState.players.end(); it++) {
		Player& p=it->second;
		LerpCycle c;
		c.waitForRespawn_before = 0;
		c.waitForRespawn_after = 0;
		c.A.id = p.id;
		c.A.state.Invalidate();
		c.B.id = p.id;
		c.B.state.Invalidate();
		c.A.state.timestamp = -1.0f; /// we do not know
		c.B.state.timestamp = -1.0f; /// we do not know
		c.t=0.0f;
		interpolation.lerps.push_back(c);
		p.waitForRespawnReset = 0;
		p.waitForRespawn=c.waitForRespawn_before;
		p.origState.Invalidate();
	}

	if (interpolation.valid) {
		SimulateInterpolation();
	}
	interpolation.valid=false;
}

void Interpreter::ProcessNewPlayerResult()
{
	PlayerSnapshot p;
	uint32_t mtype = ReadUint();
	float rts = ReadFloat();
	float ts = ReadFloat();
	float now = ReadFloat();
	ReadPlayerSnapshot(p);
	p.state.timestamp=ts;
	p.state.realTimestamp=rts; // TODO encode now somewhere
	log.Log(Logger::DEBUG, "got NEW_PLAYER_RESULT for player %u: mtype %u rts %f ts %f now %f",
		(unsigned)p.id,(unsigned)mtype,rts,ts,now);

	bool isNew;
	ResultProcessorChannel *rpc = resultProcessor.GetChannel(p.id, 3, isNew);
	if (isNew) {
		rpc->SetLogger(&log);
		rpc->SetName("captured_results");
		rpc->StartStream(GetOutputDir());
		log.Log(Logger::INFO,"created new result process channel '%s'", rpc->GetName());
	}
	rpc->Add(p);
	log.Log(Logger::DEBUG_DETAIL, p);
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
		case NEW_ENQUEUE:
			ProcessNewEnqueue();
			break;
		case NEW_TIME_SYNC:
			ProcessNewTimeSync();
			break;
		case NEW_INTERPOLATE:
			ProcessNewInterpolate();
			break;
		case NEW_PLAYER_RESULT:
			ProcessNewPlayerResult();
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

	fileVersion = 0;

	if (!OpenFile(filename)) {
		return false;
	}

	process = true;
	resultProcessor.Clear();
	gameState.Reset();

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
