# NEXUS AI Diagnostics & Quick Benchmark Update

## Added
- `Check Ollama & Models` in AI Center calls the fixed local endpoint `http://127.0.0.1:11434/api/tags` and displays whether Ollama is reachable and which configured Qwen models are installed.
- `Run Quick Benchmark` sends one short deterministic prompt to the selected local model through the existing local-inference policy and loopback endpoint.
- Benchmark output reports end-to-end request time and, when Ollama returns the metadata, generated token count and generation tokens/second.
- Benchmark outcome is written to the existing local audit log. No prompt or API key is written by the benchmark.
- The UI clarifies this is a quick local measurement, not a standardized benchmark score.

## Privacy and safety
- The diagnostics check and benchmark use only `127.0.0.1:11434`; no online AI API is called.
- The benchmark is available only in `Local · Ollama` mode and only for models allowed by `AiExecutionPolicy`.
- Web search remains opt-in and still requires its existing per-message confirmation.

## Validation
- Source changes were reviewed for insertion points and string syntax.
- This environment does not have the .NET SDK, so a Windows `dotnet build` and `dotnet test` have NOT been run on this updated archive. Validate on the target Windows machine before treating it as a release.

## Windows validation
From the extracted project root:

```powershell
dotnet build .\NEXUS.sln -c Release
dotnet test .\NEXUS.sln -c Release
```

Then launch `src\Nexus.App\bin\Release\net8.0\NEXUS.exe`, open AI Center, and test `Check Ollama & Models` and `Run Quick Benchmark` with Ollama running and a configured Qwen model installed.
