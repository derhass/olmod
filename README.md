## olmod 0.3.5 - Overload mod

**Community mods for Overload**

[Overload](https://playoverload.com) is a registered trademark of [Revival Productions, LLC](https://www.revivalprod.com).
This is an unaffiliated, unsupported tool. Use at your own risk.

#### How to run

- Download the latest release from [olmod.overloadmaps.com](https://olmod.overloadmaps.com)

- Extract olmod in the Overload main directory
  (where `Overload.exe` / `Overload.x86_64` / `Overload.app` is also located).
  
- On linux / mac, execute `chmod +x olmod.sh` from the terminal after changing to the correct directory.
  For example after `cd "~/Library/Application Support/Steam/steamapps/common/Overload"`

- Run `olmod.exe` / `olmod.sh` (linux / mac) instead of `Overload.exe`

  On mac you might get a warning about `olmod.dylib`, you can add an exception in
  System Preferences, Security & Privacy, General.

- Use `olmodserver.bat` / `olmodserver.sh` (linux) to start a LAN server,
  use `olmodserverinet.bat` / `olmodserverinet.sh` (linux) to start an
  internet server. For the internet server you need to open UDP port range
  7000-8001

#### What does it do

- Allows access to the unfinished Monsterball multiplayer mode (with tweaks
  by terminal).
  The Monsterball multiplayer mode only works when both server and clients run olmod.

- Puts a MP player in observer mode if it uses a pilot name starting with
  OBSERVER.
  The observer mode only works when the server and the observer client run olmod.

- Reads projdata.txt / robotdata.txt with custom projectile (weapon) and
  robot settings. You can extract the stock data from the game with the
  included tool `olgetdata`. The txt files must be in the same directory as
  olmod.exe. You can run olgetdata on linux with `mono olgetdata.exe`.

- Adds `frametime` non-cheat code

- Adds a rearview option for all game modes, with an option to allow it in a multiplayer game

- Allows MP with up to 16 players / 8 teams (server and all clients must run olmod)

- Writes match log files on server (by luponix)

- Fixes MP client homing behaviour (by terminal)

- Allows shoot to open doors and disables door open when nearby in MP

- Allows pasting in the MP password field

- Allows joining in progress matches when enabled for the match

- Adds option to enable console with ` key

- Adds console commands: xp, reload_missions, mipmap_bias, ui_color

- Allows custom music in custom levels

- Allows powerup spawn data for custom MP levels

- Adds Internet match option with built in olproxy

- Adds -frametime command line option

- Adds custom mod loading, add Mod-xxx.dll assembly to olmod directory

- Automatically downloads MP levels from overloadmaps.com

- Adds MP level select screen, sorts MP level list

- Disables weapon speed/lifetime randomization in LAN/Internet MP

- Adds Capture The Flag and Race match mode (Race mode by Tobias)

- MP Prev weapon switch fix from Tobias

- Adds support for some missing textures (crystal) and props (fans, monitors) in custom levels

- MP server writes kill/damage log and optionally send it to tracker

- Always makes all MP loadouts available and uses Impulse instead of Reflex for Warrior

- Fixes missing doors and teleporters in MP levels

- Less strict item pickup for inverted segment MP levels, originally by terminal

- Adds MP presets by Tobias

- Allows more simultaneous sounds for large MP matches

- Adds server browser by Tobias

- Replaces default networking model with a sniper packets style, resulting in more consistent network play.  Older clients can still play with the newer clients, but won't experience the new networking model.

- Better synchronizes energy, ammo, weapon choice, missile counts, and devastator firing/triggering when using sniper packets.

- Shows you in the version whether you are running a modded version of olmod or not.

- Improves the primary spawning algorithm.

- Adds weapon and missile autoselect for multiplayer games, by luponix.

- Adds a multiplayer game option to scale respawn time by team size, by Tobias.

- Adds a Classic Spawns game mode, which makes the game play like other 6DoF multiplayer games such as Descent 3, by Tobias.

- Adds an option to allow or disallow boosting when running the flag in CTF, by Tobias.

- Adds an option to allow or disallow specific modifiers for games, by Tobias.

#### How to build

##### Windows

- Open solution in Visual Studio 2017 or 2019

- In the GameMod project, find the file Directory.Build.targets and replace the OverloadDir variable with the path to Overload 1.1 on your machine.

- Run

##### Linux

Build the shared object library:

```
$ cd linux
$ make olmod.so
```

Build the GameMod.dll file containing the game logic:

```
$ cd linux
$ export OLPATH=/path/to/your/Overload/installation
$ make GameMod.dll
```

Please note that you will need the Mono C# compiler for this to work.

##### Mac OS

Build the dynamic library:

```
$ cd linux
$ make olmod.dylib
```

Build the GameMod.dll file containing the game logic (untested):

```
$ cd linux
$ export OLPATH=/path/to/your/Overload/installation
$ make GameMod.dll
```

Please note that you will need the Mono C# compiler for this to work.


#### How does it work

The regular `Overload.exe` just runs `UnityMain` from `UnityPlayer.dll`.
The replacement `olmod.exe` also runs `UnityMain`, but intercepts calls from Unity to the mono C# engine
to also load `GameMod.dll`. The file `GameMod.dll` contains a C# class that
uses [Harmony](https://github.com/pardeike/Harmony) to modify the game
scripting code in memory.
