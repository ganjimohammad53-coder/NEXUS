NEXUS AI CHAT PATCH (Windows / .NET 8)

This patch replaces the existing AI Center implementation; it does not add a second sidebar AI tab. Chat and Gaming AI are views inside the same AI workspace. Messages are sent only to local Ollama at 127.0.0.1:11434/api/chat. Chat history is stored in the NEXUS local data folder as ai-chat-history.json. No online provider is used.

Apply: back up src\Nexus.App\MainWindow.axaml.cs, then replace it with the file at src\Nexus.App\MainWindow.axaml.cs from this archive. Open PowerShell at the repository root and run:
  dotnet restore .\src\Nexus.App\Nexus.App.csproj
  dotnet build .\src\Nexus.App\Nexus.App.csproj -c Release

To use chat, install/start Ollama locally and download a model (for example: ollama pull llama3.2). Ollama must be listening on 127.0.0.1:11434. This patch was not compiled in this environment; run the build above and share any errors for a targeted fix.
