# NEXUS Security & Backup Update

## Changes in this update
- Added an audit-log health check that validates each record's timestamp, four-column format, target-hash field, readability and writability.
- Audit writes now flush the record and request a disk flush before reporting success.
- Added tests for healthy audit logs and malformed audit records.
- Fixed mojibake in the Security page audit explanation.
- Clarified that a SHA-256 manifest stored beside a backup detects mismatch/corruption but is not a defense against an attacker who can rewrite both the backup and its manifest.
- Replaced the misleading restore action with a clear blocked state. Database restore remains disabled in the UI until an OS-isolated security broker is available; a same-process approval token is not a security boundary.

## Preserved safeguards
- Backup creation remains non-overwriting and requires audit logging.
- Backup verification still checks manifest version, safe filename, file size, SHA-256 and SQLite integrity.
- Restore service still performs re-verification and pre-restore rollback checks, but the app UI does not permit live restore while the isolated broker is absent.
- Plugin execution, admin elevation and arbitrary automation remain disabled.

## Validation status
- Source edits and test additions are included in this archive.
- This environment does not have the .NET SDK available, so this update has **not** been compiled or test-run here. Run on Windows from the folder containing `NEXUS.sln`:

```powershell
dotnet build .\NEXUS.sln -c Release
dotnet test .\NEXUS.sln -c Release
```

- Expected test count should increase by two, from 50 to 52, if no other test changes are present. This is an expectation, not a claimed result.
