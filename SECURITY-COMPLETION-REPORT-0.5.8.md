# NEXUS Security Completion Pass — 0.5.8

## Changes in this pass
- Fixed the policy/UI mismatch: `restore-database` is not an always-disabled action; it still requires an exact target-bound, expiring, one-use approval after the explicit restore confirmation. Plugin execution, elevation, online transmission, and automation execution remain disabled.
- Bounded the in-memory replay cache by removing expired approval nonces during authorization.
- Used a GUID suffix for rollback snapshots to avoid same-millisecond filename collisions without overwriting an existing recovery file.
- Added best-effort cleanup for partial backup and manifest files after failed backup creation.
- Rejects backup database files and manifests that are direct reparse points, and snapshot verification rejects a direct reparse-point file.
- Added tests for empty approval nonce, excessive approval lifetime, and symlink snapshot rejection where the host supports symlink creation.
- Updated the security model and the Security UI limitation text.

## Important remaining limitations
- The permission service and confirmation UI are in the same process. This is not an OS-enforced authorization boundary; a compromised process can bypass in-process checks.
- The UI creates its approval grant itself after the user confirms. This protects against accidental operations in the normal UI flow, but is not independent proof of user intent against a compromised process.
- Direct file reparse points are rejected, but a complete Windows-safe implementation must inspect every ancestor path component and use handle-based APIs to eliminate junction/symlink TOCTOU races.
- Audit log is not append-only or cryptographically chained and can be altered by a sufficiently privileged local process.
- Restore requires a single NEXUS instance; concurrent app instances and abrupt power loss need further integration testing.
- No build or xUnit execution was possible in this environment because `dotnet` is not installed. ZIP integrity and source-level checks do not establish successful compilation or runtime security.

## Required validation on Windows
1. Run `dotnet restore NEXUS.sln`.
2. Run `dotnet build NEXUS.sln --no-restore`.
3. Run `dotnet test NEXUS.sln --no-build` (or test project explicitly if solution configuration excludes tests).
4. On a disposable profile, create backup, modify data, restore, and verify data returns to the snapshot.
5. Repeat with tampered manifest, altered DB bytes, blocked audit-log path, and a symlink backup where supported.
6. Confirm plugin execution, arbitrary automation execution, online transmission and elevation stay denied.

This is an Alpha security hardening pass, not a claim that the product is fully secure or production-ready.
