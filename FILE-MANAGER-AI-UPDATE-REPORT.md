# NEXUS File Manager + Local AI update

## Changes
- Added Home and Up one level navigation controls to File Manager.
- Added a Local AI File Assistant panel with a user-supplied question.
- The assistant sends only a bounded list of filenames, extensions, directory flags, and file sizes (up to 100 entries) to local Ollama at `127.0.0.1:11434`; it does not read file contents, send metadata to the web, or change files.
- Added prompt rules treating filenames as untrusted data and requiring non-destructive suggestions only.
- Added a result dialog and audit events for local metadata analysis.
- Hardened Open behavior by blocking common executable, script, shortcut, installer, and system-sensitive extensions, and checking the selected path for reparse points.
- Corrected the AI welcome message so it describes local Ollama and optional web search, rather than incorrectly mentioning online OpenAI in that message.
- Preserved the existing permission policy. File creation/rename/delete are still subject to fail-closed authorization and cannot be represented as fully available until an isolated security broker exists.

## Validation status
- Source edits applied to `src/Nexus.App/MainWindow.axaml.cs`.
- Static source review only; this environment does not have the .NET SDK, so compilation, automated tests, and Windows UI runtime tests have not been run here.

## Windows verification
Run from the folder containing `NEXUS.sln`:

```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Release
dotnet test .\NEXUS.sln -c Release
```

Then open File Manager, browse to a test folder with non-sensitive filenames, ask the local AI assistant for a summary, and verify that Ollama is running. Confirm that no files are changed by the analysis.
