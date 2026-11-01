# NEXUS Alpha 0.1 — Windows 10 compatibility edition

## Compatibility changes in this bundle

- Pinned the SDK to .NET `8.0.426` in `global.json`, matching the SDK available on the target PC.
- Changed both projects from `net10.0` to `net8.0`.
- Changed Avalonia packages from `12.1.4` to `11.3.21` to use the Avalonia 11 XAML/compiler generation stack with .NET 8.
- Changed `Microsoft.Data.Sqlite` from `10.0.13` to `8.0.13` to align the SQLite provider with .NET 8.
- Updated setup guidance to request .NET 8 rather than .NET 10.
- Legacy Python prototype copies remain preserved and are not used by the new app.

## Verification status

The authoring container does not have the `dotnet` command, so `dotnet restore`, `dotnet build`, `dotnet test`, and launching the Windows UI could not be run here. This bundle is a compatibility adaptation, **not a claim of a successful build**.

## First run on Windows 10

1. Extract this ZIP to a normal user folder.
2. Open the extracted `NEXUS-alpha` folder.
3. Run `RUN-NEXUS.bat`, or open PowerShell in that folder and run:

```powershell
 dotnet --version
 dotnet restore .\NEXUS.sln
 dotnet build .\NEXUS.sln -c Debug --no-restore
 dotnet test .\NEXUS.sln -c Debug --no-build
 dotnet run --project .\src\Nexus.App\Nexus.App.csproj
```

If the build fails, save the complete first error and stop there; do not install services, elevate privileges, or change machine-wide settings.


## Follow-up fixes (Windows 10 compatibility package)
- Fixed SQLite backup snapshot file-handle contention by disabling SQLite connection pooling for the temporary backup destination and snapshot-verification connections. This allows the SHA-256 file read to proceed after snapshot verification closes its connection.
- Added a public parameterless `MainWindow` constructor for Avalonia's runtime XAML loader warning; normal application startup continues to use the shared injected services.
- These source changes require `dotnet build` and `dotnet test` on the target Windows machine to confirm.


## Follow-up implementation pass

- Added a real Network Center view using local Windows network-interface metadata (status, link rate, and visible IP addresses). No scans or network configuration changes are performed.
- Added a read-only Gaming Center view that lists visible game/launcher-like processes. This is not claimed to be a complete installed-game library or performance-tuning system.
- Dashboard active-module count is now calculated from module statuses rather than hard-coded.
- Verification remains pending on the target Windows PC because this environment does not provide the .NET SDK.


## Security work — 0.5.6
- Added verified backup listing and manifest/hash/size/SQLite-integrity revalidation.
- Added restore of the newest verified backup with explicit irreversible confirmation and a temporary rollback snapshot.
- Added rollback attempt and preserves the rollback artifact if rollback cannot be verified.
- Audit logging now reports write success/failure; sensitive file operations, task deletion and backup/restore fail closed when audit checks fail.
- Added tests for restore approval, backup restore round-trip and tampered backup rejection.
- Build/test status: not verified in this environment; run `dotnet test .\NEXUS.sln` on Windows with the pinned .NET 8 SDK before relying on this build.


## Deep debug pass — 0.5.7
- Restore now creates the rollback destination when needed, verifies the rollback snapshot before mutating the live database, and revalidates the selected backup immediately before restore.
- Restore rejects changed backup metadata (path, hash, byte count, timestamp) and malformed/non-hex SHA-256 manifest values.
- Restore failures are recorded to the audit log; rollback artifacts are retained when rollback cannot be confirmed or its audit event fails.
- The backup-list UI now catches enumeration/verification exceptions and reports audit-log write failures instead of allowing an event-handler exception to escape.
- Task deletion now checks whether the requested task still exists and warns if post-operation audit logging fails.
- Added tests for restoring when the rollback directory is initially absent, malformed manifests, and non-hex manifest hashes.
- Static archive/XML/source checks only. The current environment does not have the .NET SDK; build, xUnit tests, Windows UI behavior, concurrency, and failure-injection tests remain unverified.

## Known security limitations after this pass
- Permission approval is still an in-process policy gate; it is not a defense against a compromised NEXUS process or another process running as the same Windows user.
- Restore asks the user to close other NEXUS instances but does not enforce a cross-process exclusive database lock. Multiple instances can still race.
- File operations still have a time-of-check/time-of-use window, especially around reparse points (symlinks/junctions); this alpha must not be described as a hardened file-operation sandbox.
- The audit log is write-checked but is not cryptographically tamper-evident or protected with Windows ACL hardening.
- Restore currently targets the latest verified backup in the UI; selecting an older backup and a separate recovery workflow remain future improvements.
- These limitations are explicit and should be addressed before production/release claims.


## Security pass 0.5.8

Source-level hardening added: bounded replay cache, explicit restore authorization path verified by existing unit test, unique rollback filenames, cleanup attempts for partial backups, direct file reparse-point rejection, and additional permission/symlink tests. This pass has not been compiled or executed because the authoring environment lacks the .NET SDK. See `SECURITY-COMPLETION-REPORT-0.5.8.md`; do not treat this status as a release sign-off.


## 0.5.9 UI update note
The AI Center now has a custom AI Studio shell and subtle page fade. This update is not build-verified in the authoring environment; verify on Windows. AI execution remains disabled.
