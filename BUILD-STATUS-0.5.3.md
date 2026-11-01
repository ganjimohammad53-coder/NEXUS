# NEXUS 0.5.3 — module implementation update

## Changes in this package

- Customization: retained dark-first palette and persistent accent/density/sidebar/startup preferences; added persistent Overview welcome-panel and toolkit visibility preferences. Reset customization restores these dashboard preferences.
- Automation: added locally persisted workspace recipes. A recipe can navigate to an approved NEXUS page after an explicit confirmation. Recipes do not execute shell commands, scripts, background tasks, or unattended actions.
- File Manager: added folder navigation, open-in-Explorer, opening files through the OS default handler, create-folder, rename and permanent delete. Mutating actions show the exact target and require explicit confirmation; operations are logged locally. Folder listing remains capped at 200 entries and does not read file contents.

## Important limitations

- This is not a claim that the product is release-ready. File operations are not protected by OS-level sandboxing, and the permission service is in-process. Keep high-risk operations limited to trusted paths and review every confirmation.
- Automation currently means saved workspace navigation recipes only. Arbitrary command/script execution and unattended automation remain disabled.
- This environment did not have the .NET SDK available, so a real `dotnet build` and test run could not be performed here. Run `RUN-NEXUS.bat` on the target Windows 10 machine and review all build/test output before considering release.
