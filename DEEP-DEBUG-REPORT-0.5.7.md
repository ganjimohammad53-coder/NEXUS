# NEXUS 0.5.7 — Deep Debug Report

## Scope reviewed
- App shell and page routing
- File Manager create/list/open/rename/delete paths
- Automation recipe persistence and navigation-only behavior
- Permission policy and one-time grants
- SQLite schema access, snapshot and restore paths
- Backup manifests, SHA-256 verification and audit events
- Existing automated tests and packaging scripts

## Changes in this pass
1. Backup restore now rejects stale metadata (database path, hash, byte count and creation time) before touching the live database.
2. Manifest SHA-256 is checked for exactly 64 hexadecimal characters before comparison.
3. Restore creates the rollback directory when absent and requires a successful integrity check of the rollback snapshot before changing the live database.
4. The chosen backup is revalidated immediately before restore to reduce a time-of-check/time-of-use window.
5. Restore failures are audited where possible. If rollback cannot be confirmed or its audit event cannot be recorded, the recovery database is retained and its path is reported.
6. Snapshot destinations are atomically reserved with `FileMode.CreateNew` before SQLite opens them, reducing the chance of a concurrent operation causing an existing file to be mutated.
7. The backup verification UI handles enumeration exceptions and explicitly reports audit-log write failure.
8. Task deletion checks the database result and reports when post-delete audit logging fails.
9. Added tests for malformed manifests, non-hex hashes, missing rollback directories, audit-log failure, and stale backup metadata.

## Static checks performed
- XML parsing passed for project files, Avalonia XAML and the application manifest.
- Source-level invariant checks passed for the new restore guards and test cases.
- ZIP integrity check passed for the generated package.

## Not verified
- `dotnet restore`, `dotnet build`, `dotnet test`, and launching the Avalonia UI: the authoring environment has no `dotnet` executable.
- Windows-specific file sharing, SQLite WAL behavior under multiple simultaneous NEXUS instances, forced process termination during restore, and failure injection.
- Full threat-model validation against junctions/reparse points and same-user malicious processes.

## Remaining risks (must not be described as solved)
- The permission gate is in-process and can be bypassed by a compromised application process.
- Restore asks users to close other NEXUS instances but has no cross-process exclusive lock.
- File operation targets can still be swapped around checks via reparse points or other races; there is no OS-level file-operation sandbox.
- Audit logs are writable event logs, not cryptographically tamper-evident records, and ACL hardening is not implemented.
- The UI restores the newest verified backup only; selecting an older backup is not implemented.
- This remains an alpha build, not a production security boundary.

## Required target-PC verification
From the extracted `NEXUS-alpha` directory on Windows with .NET SDK 8.0.426:

```powershell
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Debug --no-restore
dotnet test .\NEXUS.sln -c Debug --no-build
dotnet run --project .\src\Nexus.App\Nexus.App.csproj
```

Stop at the first build/test failure and retain the complete output. Do not test restore against important personal data; use disposable test data first.
