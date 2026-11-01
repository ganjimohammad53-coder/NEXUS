# NEXUS Overview + Settings + AI status update

## Changes
- Changed the AI Center module catalog status from `Paused in Alpha` to `Available` and clarified that the module uses local Ollama with optional consent-based web search.
- Overview now automatically checks the fixed local Ollama endpoint after rendering and displays the result in the Local AI Status card; the manual Check Local AI action remains available.
- Overview displays the saved default model and whether web search is configured as the default (each search still requires confirmation).
- Gaming recent activity no longer describes AI as paused by default; it clarifies that local Ollama must be running.
- Expanded Settings with AI default model, web-search-default preference, Open AI Center / Check Local AI actions, privacy and local data information, open data-folder action, workspace and Overview toggles, appearance shortcut, and audit-health status.
- AI Center loads and persists the selected default local model. The per-message web-search checkbox is not persisted after each message; the default preference is controlled in Settings.

## Validation and limitations
- Static source checks performed: no remaining `Paused in Alpha` or `Paused by default` labels in the updated MainWindow source; brace counts match.
- ZIP integrity checked after packaging.
- This environment does not have the .NET SDK (`dotnet` command unavailable), so this update has not been compiled or run here. Please run `dotnet build .\NEXUS.sln -c Release` and `dotnet test .\NEXUS.sln -c Release` on Windows before treating it as validated.
- AI availability in the Overview depends on Ollama running at `127.0.0.1:11434` and having a configured model installed. The UI status does not imply that Ollama is installed or running.
