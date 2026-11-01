# NEXUS AI Center — dual-provider implementation report

## Implemented in this source patch
- Added a provider selector with separate Local Ollama and Online OpenAI API modes.
- Local mode is restricted by an explicit policy check to `http://127.0.0.1:11434/api/chat` and the two model names already exposed in the UI.
- Online mode uses the fixed HTTPS endpoint `https://api.openai.com/v1/chat/completions` and a small model allowlist.
- Each online message requires a confirmation dialog explaining that the current prompt and online conversation history leave the device and that API usage may incur charges.
- The OpenAI API key is entered for the current app session only; this patch does not persist it to a settings file or chat history.
- Local and online chat histories use separate files. Local conversation history is never included in online requests.
- HTTP redirects are disabled for both provider clients. Raw provider error bodies are not copied into chat history.
- Added policy tests for local endpoint restrictions, online per-message consent, key presence, HTTPS host/path restrictions, and model allowlists.
- Repaired the visible mojibake label for the jump-to-latest-messages button.

## Before using online mode
1. The user must have OpenAI API access and a valid API key. ChatGPT subscriptions and API billing are separate.
2. API calls may incur usage charges. Review model pricing and usage limits before sending messages.
3. Never paste an API key into chat, source code, screenshots, or a public repository.
4. Online mode transmits the current prompt and this provider's own chat history to OpenAI only after the per-message confirmation.

## Validation status
- Source edits and targeted test cases have been added.
- A .NET SDK was not available in the editing environment, so compilation, xUnit execution, and Windows UI smoke tests have NOT been run here.
- Run from the project root on Windows:

```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Release --no-restore
dotnet test .\NEXUS.sln -c Release --no-build
```

This is a source patch, not a claim of a tested release binary. The online feature cannot make a live request until a valid API key is supplied in the app and the machine has network access.


## User-approved local AI web search

- Added an opt-in per-message web-search checkbox in the local Ollama AI Center. It is disabled and cleared when Online OpenAI mode is selected.
- Before a search request, NEXUS displays the exact query and asks for explicit confirmation. Cancel sends no query.
- Search is performed through the fixed DuckDuckGo HTML endpoint over HTTPS. Only titles, URLs, and snippets are parsed; NEXUS does not automatically open result pages or execute page scripts.
- Search consent is recorded in the local audit log; if audit logging fails, the search is blocked.
- Search results appear as source cards with title, snippet, URL, and an Open source button. Only the result snippets are provided to the local Ollama model. The model is instructed to treat results as untrusted reference data and cite them as [1], [2], etc.
- The feature depends on access to DuckDuckGo from the user's network. No search provider can be guaranteed reachable in every region/network.
- This is source-level implementation. Build and test on the target Windows machine before treating it as a verified release.


## No-paid-AI-API mode (user-approved conversion)

- The visible second mode is now `AI + Web Search`, not `Online · OpenAI API`.
- Both modes use the installed local Ollama models (`qwen2.5:14b` and `qwen3.5:4b`).
- Selecting `AI + Web Search` enables per-message search; the exact query is shown in a confirmation dialog before DuckDuckGo is contacted.
- No OpenAI API key is needed for this workflow. Search-provider availability depends on the user's network and regional access.
- The legacy OpenAI request code remains in the source but is not selectable/reachable through the current UI (`IsOnlineAi` is fixed false); it should be removed in a later cleanup if desired.
- Rebuild and run tests on Windows before replacing the current project.
