# NEXUS Alpha — Security Model (0.5.6)

## Implemented protections
- Local-first: no online AI requests, no silent data transmission, no administrator elevation, and no arbitrary plugin/script execution.
- Permission grants are bound to exact module, action and target, expire within two minutes, and are one-use. Replay tracking drops expired nonces to bound memory growth. Disabled capabilities remain denied even with a grant.
- **Fail-closed containment:** all requests marked irreversible are denied by the in-process permission service until an OS-isolated broker is available. This intentionally disables destructive file actions, task deletion and database restore in this candidate.
- File rename/delete and database restore require explicit, target-specific confirmation. Recursive folder deletion remains disabled. File mutation is blocked when the local audit log cannot be written.
- Audit events contain event category, result and a short SHA-256 target fingerprint; they do not intentionally record file contents, passwords or tokens. The UI exposes an audit-write health check.
- Database backups use SQLite's backup API, integrity checks, a SHA-256 manifest and collision-safe new filenames. Existing backups are not overwritten. Failed backup creation attempts best-effort cleanup of partial artifacts.
- Restore only accepts a backup whose manifest format, filename, size, SHA-256 and SQLite integrity check validate. It revalidates immediately before restore and creates a uniquely named temporary snapshot of the live database first. If restore fails, it attempts rollback; if rollback cannot be verified, the rollback artifact is preserved and its path is reported.
- Tamper detection and restore/rollback flows have automated tests.

## Important limits
- The permission service still runs in the NEXUS process. It is not an independent security boundary against a compromised process or malicious local administrator. The current candidate mitigates this by denying all operations marked irreversible; this is containment, not a replacement for an OS-isolated broker.
- The local audit log is not cryptographically chained or remotely protected; a user/process with sufficient local access can alter it.
- SHA-256 manifests detect accidental corruption and edits only when the manifest itself remains trusted; they are not a digital signature. Keep backups on a separate trusted location for stronger recovery.
- Restore is intended for a single running NEXUS instance. Close other NEXUS instances before restoring. A power loss or filesystem failure can still require manual recovery from a preserved rollback file.
- Windows handle-based file mutation, reparse-point/symlink-safe operations, a separately installed broker with OS-enforced isolation/ACLs, signed updates, OS-enforced plugin sandboxing, and full threat-model review remain future release requirements. File mutation is not enabled until those controls are implemented and tested.
- No security-sensitive software can be declared fully secure based only on unit tests or source inspection. Perform a clean Windows build, run the full test suite, and test restore on a disposable copy before production use.


## NEXUS 1.0 AI policy
- AI execution is hard-disabled in this build. There is no local inference runtime, model download path, online provider client, AI memory store, or AI tool executor.
- `AiExecutionPolicy` centrally denies known AI actions regardless of which UI module requests them; the `AI Center` module itself is always denied by the permission service. A UI approval token cannot override this policy.
- The AI Center is a readiness/status interface only. Its runtime stage is marked locked rather than implying that model discovery or inference already works.
- Before enabling AI in a later release, add an explicit local-runtime integration, threat-model it, test prompt/file boundaries, and implement separate per-request consent for any online provider.
