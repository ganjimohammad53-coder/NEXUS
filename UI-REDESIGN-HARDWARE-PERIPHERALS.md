# NEXUS UI Update — System & Hardware + Peripherals

## Changes
- Reworked the System & Hardware page into a compact overview with a purple hero header, refresh/Device Manager actions, four summary cards, inventory timestamp/telemetry note, and two-column hardware detail cards.
- Reworked Peripherals into a device summary row, clearer inventory toolbar, category cards with device counts, per-device manufacturer/status presentation, and a dedicated note about vendor-specific controls.
- Preserved the existing Windows inventory source and did not invent temperatures, fan speed, RGB/DPI controls, battery data, or other unavailable telemetry.
- Kept existing navigation routes and refresh behavior.

## Validation
- Static brace/parenthesis count check passed for `MainWindow.axaml.cs`.
- .NET build/test were not run in this environment because the `dotnet` CLI is unavailable. Run `dotnet build .\NEXUS.sln -c Release` and `dotnet test .\NEXUS.sln -c Release` on Windows before treating the update as verified.
