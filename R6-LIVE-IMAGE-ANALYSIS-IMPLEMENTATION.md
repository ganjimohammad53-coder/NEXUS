# Rainbow Six Siege live image analysis — implementation notes

## Added to Gaming Center
- Start/stop periodic live visual analysis, sampling about every 15 seconds.
- Analyze one current game frame on demand.
- Keep manual screenshot analysis as a fallback.
- Captures the R6 game window only when it is the foreground window; no game injection or memory inspection.
- Sends JPEG frame data to local Ollama at `127.0.0.1:11434/api/chat` using the selected model and existing local-inference policy.
- Records successful analysis in the existing audit log and displays capture/model errors in the UI.

## Important limits
- This is sampled visual analysis, not video understanding and not official game telemetry. It cannot know hidden enemies, off-screen events, precise game state, or values unreadable in a frame.
- A vision-capable Ollama model is required; text-only models will fail on image requests.
- The game must be running in a visible foreground window. Protected/exclusive fullscreen capture may fail or return a black frame; if so, use borderless/windowed mode.
- Sampling is intentionally limited to about every 15 seconds to limit GPU/VRAM use. It is not suitable for frame-perfect or latency-critical decisions.
- No automated input, game-memory reading, anti-cheat bypass, or exploit functionality is added.

## Build/test status
Source and project-file checks were performed in the packaging environment, but `dotnet` was not available there, so this patch has **not** been compiled or run in this environment. Run restore/build/tests on Windows before replacing any existing stable build.
