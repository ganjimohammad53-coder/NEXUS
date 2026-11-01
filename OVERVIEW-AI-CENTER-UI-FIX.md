# NEXUS Overview + AI Center UI update

## Overview
- Added an AI Center feature card to the Overview dashboard.
- Added a Local AI Status panel with **Open AI Center** and **Check Local AI** actions.
- Local status check uses only `http://127.0.0.1:11434/api/tags` and reports installed model names; it does not call an online AI provider.

## AI Center
- Made the provider/model/new-chat toolbar wrap on narrower windows instead of requiring one wide fixed grid.
- Set `qwen3.5:4b` as the default selection, followed by `qwen2.5:14b`, to better match a lightweight local setup. The selected model must actually be installed in Ollama.
- Kept the local diagnostics and quick benchmark controls, with clearer benchmark guidance.

## Validation
- Source edits were reviewed and archive integrity checked.
- A .NET SDK is not available in this build environment, so this archive has **not** been compiled or tested here. On Windows, close any running NEXUS window and run:

```powershell
dotnet build .\NEXUS.sln -c Release
dotnet test .\NEXUS.sln -c Release
```
