# NEXUS 1.0 — File Manager & Automation hardening report

## Changes implemented

### File Manager
- Added live name filtering and four sorting modes: name ascending/descending, folders first, files first.
- Added refresh and a per-entry exact-path copy-to-clipboard action.
- Improved empty-state messaging when a filter has no matches.
- Kept the 200-entry listing cap and metadata-only listing; the app does not read file contents for the directory view.
- Kept destructive operations fail-closed. Folder creation, rename, file deletion, and recursive folder deletion are not presented as security-complete; irreversible changes remain blocked by the current permission service.

### Automation
- Added a fixed allowlist of in-app destinations and exposed Automation itself as a valid destination.
- Added validation when loading and saving stored recipes: ID format, duplicate IDs/names, name length, destination allowlist, and a maximum of 50 recipes.
- Invalid or corrupted recipe data is preserved and reported instead of being treated as an empty list and silently overwritten.
- Saving/deleting is blocked when stored recipe data cannot be validated.
- Recipe execution remains user-triggered navigation only; no scripts, shell commands, startup triggers, or unattended execution are enabled.
- Recipe execution now stops if the audit log cannot record approval.

## Validation results
- Source-level checks: see the delivery summary.
- ZIP integrity: checked after packaging.
- C# build, xUnit tests, and Windows UI smoke tests: NOT RUN in this environment because `dotnet`, `csc`, `msbuild`, and Mono are unavailable.

## Important remaining security limits
- The current permission service is in-process, not an independent OS-enforced broker.
- Path/reparse-point checks do not eliminate TOCTOU races. Handle-based Windows filesystem operations and a separately isolated broker are still required before enabling destructive file operations.
- Therefore this is a hardening candidate, not a claim of complete production security or verified release readiness.
