# NEXUS R6 Screenshot Analysis — implementation note

## Added in this source patch
- Added a Gaming Center action to choose one local Rainbow Six Siege screenshot and request analysis from Ollama at `127.0.0.1:11434`.
- Allows PNG, JPG/JPEG, WEBP and BMP; rejects empty images and files larger than 10 MiB.
- Sends a single user-selected image, not continuous capture or screen recording.
- Prompts the model to distinguish visible facts from uncertain tactical interpretation and not invent hidden game state.
- Writes an audit entry after a successful response.
- Does not add game injection, memory reading, anti-cheat bypass, automated gameplay, or online AI transmission.

## Important limitations
- This is screenshot-based analysis, not a live telemetry integration like CS2 GSI.
- The selected Ollama model must support image input. A text-only model may return an error; install/select a vision-capable model if needed.
- Source changes have not been compiled or run in this editing environment. Run restore/build/tests on Windows and test with an actual R6 screenshot before relying on it.
