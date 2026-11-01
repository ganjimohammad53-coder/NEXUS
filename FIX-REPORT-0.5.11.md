# NEXUS 0.5.11 — Build Fix

## Fixed from the reported Windows build error
- Removed the invalid `using Avalonia.Animation.Transitions;` directive from `MainWindow.axaml.cs`. In the project's Avalonia version, `Transitions` is a type rather than a namespace; the existing `using Avalonia.Animation;` provides the transition types used by this file.

## Validation status
- Source-level check: the invalid using directive is absent.
- ZIP integrity and project XML can be checked locally.
- This environment does not have the .NET SDK, so a full `dotnet build`/`dotnet test` has not been run here. Please run the Windows commands below; if another compiler error appears, share the complete output.

```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Debug --no-restore
dotnet test .\NEXUS.sln -c Debug
```
