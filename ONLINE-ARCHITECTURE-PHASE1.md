# NEXUS Online Architecture — Phase 1 Foundation

## What this phase adds

- `src/Nexus.Server`: ASP.NET Core .NET 8 API.
- PostgreSQL schema for users, sessions, sync documents, and audit events.
- Email/password registration and login. Passwords are PBKDF2-SHA256 hashes; bearer tokens are random opaque values and only their SHA-256 hashes are stored server-side.
- Seven-day server sessions, logout/revocation, disabled-account checks, role-gated admin endpoints, and IP-based rate limits.
- Revision-based JSON document synchronization with a conflict response rather than silent overwrites.
- `/health/live` and `/health/ready` endpoints.
- Static web admin panel at `/` for administrator sign-in, service health, account/session/sync counts, and a limited account list.
- `deploy/docker-compose.yml` for a private PostgreSQL container and API container.
- `load-tests/nexus-1000-users.js` for an infrastructure/readiness ramp to 1,000 virtual users.
- `load-tests/nexus-1000-auth-sync.js` for authenticated sync load with 1,000 distinct test-session tokens.
- `CloudSyncClient` in the desktop app source, an optional client that keeps its bearer token in memory and rejects remote plain HTTP URLs. It is not wired into the current UI yet; local-first features continue to work as before.

## Important limits

This is an implementation foundation, not a production certification. No live server was available in this build environment, so no real 1,000-user load test has been executed and capacity is **not verified**. The readiness test measures readiness endpoint/database-pool response, not full user workflows. The auth/sync test needs 1,000 distinct test-user tokens, generated against a non-production deployment.

Before public deployment:

1. Configure a TLS reverse proxy and public hostname. Keep port 8080 private behind the proxy and never expose PostgreSQL.
2. Copy `deploy/.env.example` to `deploy/.env`; use long random unique secrets and keep `.env` out of source control.
3. Review backup/restore, retention, account recovery, email verification, MFA, abuse prevention, privacy/legal notices, and operational alerting.
4. Add migrations/versioning and automated integration tests for the server before treating its API as stable.
5. Wire the desktop UI to `CloudSyncClient` and define which local tables/documents are eligible for synchronization. Do not sync secrets, local file contents, or sensitive security data by default.
6. Run the k6 tests from a separate load-generator host, record p50/p95/p99, throughput, HTTP errors, CPU/RAM, PostgreSQL pool wait, DB CPU, and connection count. Adjust pool size/rate limits based on measured results.
7. Test at 100, 500, and 1,000 users; repeat with realistic sync payload sizes and login rates. Do not use production user accounts or production tokens for load tests.

## Start locally

1. Install Docker Desktop and ensure Docker is running.
2. Copy `deploy/.env.example` to `deploy/.env` and edit all secrets.
3. From the repository root run `docker compose --env-file deploy/.env -f deploy/docker-compose.yml up --build`.
4. Open `http://localhost:8080/` for local development only. Use HTTPS via a reverse proxy before any public exposure.
5. Check `http://localhost:8080/health/ready`.

## Load test

Readiness ramp (no auth tokens needed):

```sh
k6 run -e NEXUS_BASE_URL=https://your-test-host.example load-tests/nexus-1000-users.js
```

Authenticated sync ramp (use a disposable test database; requires 1,000 distinct temporary tokens):

```powershell
$env:NEXUS_AUTH_RATE_PER_MINUTE = "600"
.\load-tests\Provision-NexusLoadTestUsers.ps1 -BaseUrl "http://localhost:8080" -Count 1000
$env:NEXUS_BASE_URL = "http://localhost:8080"
$env:NEXUS_TEST_TOKENS_FILE = "load-tests/test-tokens.json"
k6 run .\load-tests\nexus-1000-auth-sync.js
```

The provisioning script creates test accounts in the target database and does not delete them. Use a disposable database and discard it after testing.

Prefer an environment variable or a local ignored file for tokens; never paste production tokens into shell history or commit them.
