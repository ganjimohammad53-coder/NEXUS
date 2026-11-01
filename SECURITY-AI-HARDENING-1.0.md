# NEXUS 1.0 — Security and AI hardening pass

## Changes in this pass
- Added `AiExecutionPolicy` as a central fail-closed gate for local inference, model loading/download, online provider calls, prompt/context transmission, AI memory and AI tool/file access.
- Wired the existing permission service to deny all requests from `AI Center` and known AI actions even if an in-process approval grant is supplied.
- Added AI policy tests for blocked actions, cross-module requests and unconditional AI Center denial.
- Added audit-log tests checking that target paths are hashed rather than written in plaintext, control characters are stripped from event fields, and an unwritable log returns failure.
- Updated the AI Center to mark model runtime as `LOCKED` and explicitly state that runtime discovery/inference are not implemented. This removes the earlier implication that a working model runtime already exists.
- Updated the security model with the current AI limitations and requirements before activation.

## Current status
- AI is intentionally disabled. No model execution, provider connection, prompt transmission, AI memory, or AI tool execution is available in this build.
- Security containment remains in place: irreversible file/database operations are blocked until an independent OS-enforced authorization boundary and safe filesystem mutation path exist.
- The audit log is local and writable-checkable, but it is not cryptographically chained or tamper-proof against a privileged local process.
- This pass does not claim production security or a complete AI implementation.

## Validation
- Source-level checks: run in the assistant workspace (see handoff report).
- .NET build and xUnit execution: NOT RUN here because the environment has no `dotnet` executable. These must be run on the Windows development machine.

## Windows validation commands
Run from the extracted `release_work` directory:

```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Release --no-restore
dotnet test .\NEXUS.sln -c Release --no-build
```

If the build fails, send the complete first error and the project path shown in PowerShell. Do not suppress the error or enable the blocked capabilities to make a test pass.


## Follow-up source patch: dual AI provider mode (2026-10-27)
- The AI Center now has distinct Local Ollama and Online OpenAI API modes.
- Online mode requires per-message confirmation, uses a fixed HTTPS endpoint, disables HTTP redirects, uses an API key held in memory only, and stores its chat history separately from local history.
- The online provider transmits the current prompt plus that provider's own conversation history only after confirmation. This may incur API charges.
- Local and online requests use provider-specific endpoint/model allowlists.
- This patch has not been compiled or runtime-tested because the editing environment has no .NET SDK. Windows build, xUnit and UI validation remain required.
