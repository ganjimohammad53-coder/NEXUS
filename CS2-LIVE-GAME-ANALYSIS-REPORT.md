# NEXUS Gaming — CS2 live game analysis

## Implemented in this patch
- Added `Cs2GameStateService`, a loopback-only TCP HTTP receiver for Counter-Strike 2 Game State Integration (GSI) POST updates.
- Receiver binds to `127.0.0.1:3000`, accepts bounded JSON payloads, requires the configured GSI token, and keeps the latest parsed snapshot in memory.
- Parsed fields include map/map phase/round phase, CT/T score, player activity, health, armor, money, kills/deaths/assists/MVPs/score, and active weapon when those fields are supplied by CS2.
- Added Gaming Center controls to start/stop the local listener, create a GSI config file in Documents\NEXUS, show live/stale status and received facts, and request a one-shot local Ollama analysis.
- AI analysis is constrained to the latest GSI snapshot, identifies missing fields, and must not invent FPS, ping, GPU readings, enemy positions, aim events, or unobserved match events.
- Generated GSI configuration requests own-player state only; all-player state/weapons/match-stat sections are disabled. The receiver only binds to loopback and does not inspect game memory, inject into the game, or modify game files.
- Added unit tests for snapshot parsing and GSI configuration safety.

## Manual setup
1. In NEXUS Gaming Center, click **Create GSI Config**.
2. Copy `Documents\NEXUS\gamestate_integration_nexus.cfg` to the CS2 installation folder `game\csgo\cfg` (the exact install root depends on the user's Steam library).
3. Start the NEXUS listener, then restart/launch CS2.
4. Return to Gaming Center. Wait until status reads **LIVE**, then click **Analyze Live Game**.
5. Ollama must be running locally at `127.0.0.1:11434`, and one supported model (`qwen3.5:4b` or `qwen2.5:14b`) must be installed.

## Limitations
- This first implementation provides event-driven GSI state, not screen/video analysis or a continuous AI commentary stream. Analysis runs when the user clicks the button.
- GSI does not provide FPS, frame times, server ping, GPU temperatures, or enemy position data. Those remain unavailable unless separate validated providers are implemented.
- No CS2 runtime is available in the build environment; GSI integration must be manually verified on Windows with CS2 running.
- Release build/tests were not run in this editing environment because the .NET SDK is unavailable here. Run the project's Windows build and test commands before relying on the patch.
