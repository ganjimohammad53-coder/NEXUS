# Build error fix report — follow-up

Fixed the compile error in `src/Nexus.App/MainWindow.axaml.cs` at the saved-workflows search empty-state branch. `AddText` accepts `MainWindow.CardPanel`, while `recipesList` is a `StackPanel`; replaced the incompatible call with direct `TextBlock` creation and insertion into `recipesList.Children`.

The CA1416 warnings in `HardwareInventoryService.cs` are separate platform-analysis warnings for Windows-only WMI APIs; they did not cause this build failure. Re-run `dotnet build .\NEXUS.sln -c Release` on Windows to verify.
