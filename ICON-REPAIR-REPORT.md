# NEXUS Icon Repair Report

Date: 2026-10-24

## Scope changed

- `src/Nexus.App/MainWindow.axaml.cs`
- Kept the original project tree and existing backup files; this is a working copy, not the user's source archive.

## Repairs made

- Replaced File Manager's folder/file text markers with `PathIcon` vector geometry and kept the actual filename as text.
- Replaced Network overview status glyphs with a vector status icon plus a separate status label.
- Replaced Network adapter status glyphs with vector status icons.
- Replaced Gaming Center's broken badge glyph with a vector gamepad icon.
- Replaced Steam, Discord, Blender, and VS Code quick-launch glyph/text substitutes with vector `PathIcon` geometries.
- Replaced the Gaming AI decorative glyph with the shared AI vector geometry.
- Changed customization accent selection from broken Unicode marks to vector check icons and color swatches.
- Changed reusable selected-choice buttons to use vector selection/check icons.
- Normalized common mojibake punctuation in the modified C# file to stable readable punctuation.
- Kept the existing navigation `PathIcon` vector system and module icon geometry mapping.

## Validation status

- Source-level checks: performed.
- Build/test: not run in this environment because the `dotnet` executable is unavailable here. Run the commands below on the Windows development PC before treating the patch as verified.

```powershell
dotnet build .\src\Nexus.App\Nexus.App.csproj -c Release
dotnet test .\tests\Nexus.App.Tests\Nexus.App.Tests.csproj -c Release
```

## Notes

This patch does not claim that all application behavior has been release-validated. After a successful build, visually inspect Dashboard, File Manager, Network, Customization, Gaming, and AI Center at the normal and narrow window sizes. Some text labels such as `IP`, `DNS`, `FPS`, and application names are intentional labels, not icons, and remain text.
