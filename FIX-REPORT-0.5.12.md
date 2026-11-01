# NEXUS 0.5.12 — Build Fix

## Fix based on the reported Windows compiler output
- Added `using Avalonia.Controls.Shapes;` to `src/Nexus.App/MainWindow.axaml.cs`, which brings Avalonia's `Ellipse` shape into scope and addresses CS0246 at lines 160–162.
- Updated the visible version label to `NEXUS 0.5.12`.

## Validation status
- Source check confirms the required using directive exists and the `Ellipse` constructions remain present.
- This environment does not have the .NET SDK, so this package has **not** been compiled or tested here. Please run on Windows:

```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Debug --no-restore
dotnet test .\NEXUS.sln -c Debug
```
