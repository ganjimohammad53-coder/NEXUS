# NEXUS backup review — 2026-10-27

## Scope
Reviewed `NEXUS-Backup-2026-10-27_22-39-32.zip`, including the desktop Avalonia app, server project, test project, deployment templates, and load-test scripts. This was a source/static review in an environment without the .NET SDK.

## Applied correction
- Repaired four malformed em-dash sequences in `src/Nexus.App/MainWindow.axaml.cs`, including one user-visible irreversible-action warning.

## Static validation
- Original archive integrity: passed ZIP CRC validation (no corrupt entry).
- Project/XAML XML parse: 8 files checked; all passed.
- .NET build/test: not run here; `dotnet` is not installed in this environment.
- Production readiness: not certified by this review.

## Known release blockers / limitations confirmed from source
- Destructive file mutations, task deletion, and live database restore are intentionally fail-closed until an OS-isolated broker and safe handle-based file operations are implemented. Do not bypass this policy to make buttons work.
- Local AI requires Ollama running on loopback and the selected model installed. The project exposes configured model choices, not an arbitrary model picker.
- The desktop Settings page explicitly says cloud/account sync is not connected to the desktop UI; the server foundation is a separate project and is not started by the desktop app.
- Gaming capture/recording and Mods operations are labelled unavailable; do not represent these as implemented.
- Docker and k6 benchmarking is postponed per user direction. No benchmark was run.

## Required Windows verification
From the project root, run `dotnet restore .\NEXUS.sln`, `dotnet build .\NEXUS.sln -c Release --no-restore`, and `dotnet test .\NEXUS.sln -c Release --no-build`. Then launch the Release app and smoke-test navigation, AI/Ollama, task persistence, file browsing, automation, audit health, and backup verification on disposable data.
