# NEXUS Gaming — Multi-game Live AI expansion

## Added in this patch

### Dota 2 — local GSI snapshot analysis
- Added `Dota2GameStateService`, a separate loopback-only HTTP receiver on `127.0.0.1:3001`.
- Requires a local token in the incoming GSI JSON, limits request bodies to 256 KiB, and stores only the latest snapshot in memory.
- Gaming Center now offers Start/Stop Dota Listener, Create Dota GSI Config, and Analyze Dota 2 Live.
- UI surfaces map/game state, player/hero, level, health/mana, gold, and buyback cost only when present in the GSI payload.
- Before sending telemetry to Ollama, the app strips the `auth` object from the JSON. Analysis is a user-triggered single snapshot, not continuous commentary.
- Added a unit test for the generated config's loopback endpoint and requested GSI fields.

### Minecraft Java — local log snapshot analysis
- Added a button to analyze the last 100 lines of `%AppData%\.minecraft\logs\latest.log` using local Ollama after the user clicks it.
- No log is sent to an online service. This only analyzes logged events/chat/errors; it does not observe the 3D world or hidden game state.
- Custom launcher/game directories are not auto-discovered in this first pass.

### Rainbow Six Siege and Rust
- Added clear Gaming Center integration status text rather than claiming a live telemetry feed that is not available in this patch.
- R6 needs a validated supported telemetry provider or user-approved image/screenshot analysis path.
- Rust needs a dedicated server/plugin bridge for meaningful live game events. This patch deliberately does not ask for or store RCON credentials.

## Safety / limitations
- All telemetry receivers bind to loopback only.
- No process injection, game memory reading, anti-cheat bypass, or game file modification is performed. Config files are written under Documents\NEXUS and must be copied by the user.
- CS2 and Dota 2 analysis is user-triggered and based on the latest GSI snapshot.
- Ollama must be running locally and a supported model must be installed.
- This editing environment has no .NET SDK, so Release build and tests have not been executed here. Run `dotnet restore .\NEXUS.sln`, `dotnet build .\NEXUS.sln -c Release`, and `dotnet test .\NEXUS.sln -c Release` on Windows before using the patch.
