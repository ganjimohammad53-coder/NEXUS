# NEXUS 0.5.9 — AI Studio & UI polish

## Included
- Replaced the generic AI Center placeholder with a custom AI Studio workspace.
- Added honest runtime, local-first data policy, and permission status cards.
- Added a staged roadmap for model runtime, user-controlled context, and provider/tool permissions.
- Added navigation shortcuts to Security and Customization, plus an explanation of why AI is paused.
- Added a short page fade transition and kept effects lightweight.

## Safety behavior
- No model inference was added.
- No online AI provider, prompt transmission, plugin execution, or AI-driven system action was enabled.
- The page clearly states that activation waits for scoped permission, consent, and isolation work.

## Validation
- Source was updated in the existing .NET 8 / Avalonia 11 project.
- This environment did not run `dotnet build` or launch the Windows app. Build/test and visual smoke testing must be done on the target Windows machine.

## Windows verification
```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Debug --no-restore
dotnet test .\NEXUS.sln -c Debug
dotnet run --project .\src\Nexus.App\Nexus.App.csproj
```

## Manual UI checks
1. Open AI Center from the sidebar.
2. Confirm all cards fit at the default window size and at minimum window size.
3. Confirm “View security foundation” opens Security.
4. Confirm “Workspace preferences” opens Customization.
5. Confirm “Why is AI paused?” displays the honest disabled-state explanation.
6. Navigate between pages and verify the fade is subtle and no controls disappear or become unresponsive.
