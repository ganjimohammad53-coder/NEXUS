using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = Environment.GetEnvironmentVariable("NEXUS_DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Nexus")
    ?? throw new InvalidOperationException(
        "Database configuration is missing. Set NEXUS_DATABASE_URL or ConnectionStrings:Nexus.");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(databaseUrl);
await using var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

var allowedOrigins = (Environment.GetEnvironmentVariable("NEXUS_ALLOWED_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = int.TryParse(Environment.GetEnvironmentVariable("NEXUS_AUTH_RATE_PER_MINUTE"), out var limit) ? Math.Clamp(limit, 10, 600) : 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = int.TryParse(Environment.GetEnvironmentVariable("NEXUS_GLOBAL_RATE_PER_MINUTE"), out var limit) ? Math.Clamp(limit, 1000, 250000) : 100000,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();
app.UseRateLimiter();
if (allowedOrigins.Length > 0) app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

await InitializeDatabaseAsync(dataSource);
await BootstrapAdminAsync(dataSource);

app.MapGet("/health/live", () => Results.Ok(new { status = "live", service = "NEXUS.Server" }));
app.MapGet("/health/ready", async (NpgsqlDataSource db, CancellationToken ct) =>
{
    try
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ready", database = "connected" });
    }
    catch
    {
        return Results.Json(new { status = "not_ready", database = "unavailable" }, statusCode: 503);
    }
});

app.MapPost("/api/auth/register", async (RegisterRequest request, NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var email = NormalizeEmail(request.Email);
    if (!IsValidEmail(email) || request.Password is null || request.Password.Length < 12 || request.Password.Length > 256)
        return Results.BadRequest(new { error = "Enter a valid email and a password of at least 12 characters." });
    if (request.Password.Any(char.IsControl)) return Results.BadRequest(new { error = "Password contains unsupported characters." });

    var userId = Guid.NewGuid();
    var salt = RandomNumberGenerator.GetBytes(16);
    var hash = HashPassword(request.Password, salt);
    try
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO users(id,email,password_hash,password_salt,role,created_at) VALUES (@id,@email,@hash,@salt,'user',now())", conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("salt", salt);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    catch (PostgresException ex) when (ex.SqlState == "23505")
    {
        return Results.Conflict(new { error = "An account with this email already exists." });
    }
    await WriteAuditAsync(db, userId, "auth.register", context.Connection.RemoteIpAddress?.ToString(), ct);
    var token = await CreateSessionAsync(db, userId, ct);
    return Results.Created("/api/me", new AuthResponse(token, DateTimeOffset.UtcNow.AddDays(7), new UserDto(userId, email, "user")));
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/login", async (LoginRequest request, NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var email = NormalizeEmail(request.Email);
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("SELECT id,email,password_hash,password_salt,role FROM users WHERE email=@email AND disabled=false", conn);
    cmd.Parameters.AddWithValue("email", email);
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct))
    {
        await reader.DisposeAsync();
        await WriteAuditAsync(db, null, "auth.login_failed", context.Connection.RemoteIpAddress?.ToString(), ct);
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
    }
    var userId = reader.GetGuid(0);
    var storedEmail = reader.GetString(1);
    var storedHash = (byte[])reader[2];
    var salt = (byte[])reader[3];
    var role = reader.GetString(4);
    await reader.DisposeAsync();
    var candidate = HashPassword(request.Password ?? "", salt);
    if (!CryptographicOperations.FixedTimeEquals(candidate, storedHash))
    {
        await WriteAuditAsync(db, null, "auth.login_failed", context.Connection.RemoteIpAddress?.ToString(), ct);
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
    }
    var token = await CreateSessionAsync(db, userId, ct);
    await WriteAuditAsync(db, userId, "auth.login", context.Connection.RemoteIpAddress?.ToString(), ct);
    return Results.Ok(new AuthResponse(token, DateTimeOffset.UtcNow.AddDays(7), new UserDto(userId, storedEmail, role)));
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/logout", async (NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var token = GetBearerToken(context);
    if (token is null) return Results.Unauthorized();
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("DELETE FROM sessions WHERE token_hash=@hash", conn);
    cmd.Parameters.AddWithValue("hash", HashToken(token));
    await cmd.ExecuteNonQueryAsync(ct);
    return Results.NoContent();
});

app.MapGet("/api/me", async (NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var user = await AuthenticateAsync(db, context, ct);
    return user is null ? Results.Unauthorized() : Results.Ok(new UserDto(user.Id, user.Email, user.Role));
});

app.MapGet("/api/sync", async (NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var user = await AuthenticateAsync(db, context, ct);
    if (user is null) return Results.Unauthorized();
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("SELECT document_key, payload::text, revision, updated_at FROM sync_documents WHERE user_id=@uid ORDER BY document_key", conn);
    cmd.Parameters.AddWithValue("uid", user.Id);
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    var docs = new List<SyncDocumentDto>();
    while (await reader.ReadAsync(ct))
        docs.Add(new SyncDocumentDto(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetFieldValue<DateTimeOffset>(3)));
    return Results.Ok(docs);
});

app.MapPut("/api/sync/{key}", async (string key, SyncWriteRequest request, NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var user = await AuthenticateAsync(db, context, ct);
    if (user is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(key) || key.Length > 120 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':')))
        return Results.BadRequest(new { error = "Invalid document key." });
    if (request.Payload is null || Encoding.UTF8.GetByteCount(request.Payload) > 1_000_000)
        return Results.BadRequest(new { error = "Payload is required and must be at most 1 MB." });
    try { using var parsed = JsonDocument.Parse(request.Payload); }
    catch (JsonException) { return Results.BadRequest(new { error = "Payload must be valid JSON." }); }
    if (request.ExpectedRevision < 0) return Results.BadRequest(new { error = "Expected revision cannot be negative." });

    await using var conn = await db.OpenConnectionAsync(ct);
    await using var tx = await conn.BeginTransactionAsync(ct);
    await using (var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@lockkey, 0))", conn, tx))
    {
        advisory.Parameters.AddWithValue("lockkey", $"{user.Id}:{key}");
        await advisory.ExecuteNonQueryAsync(ct);
    }
    await using var read = new NpgsqlCommand("SELECT revision FROM sync_documents WHERE user_id=@uid AND document_key=@key FOR UPDATE", conn, tx);
    read.Parameters.AddWithValue("uid", user.Id);
    read.Parameters.AddWithValue("key", key);
    var current = await read.ExecuteScalarAsync(ct);
    var revision = current is null ? 0L : Convert.ToInt64(current);
    if (revision != request.ExpectedRevision)
        return Results.Conflict(new { error = "Revision conflict. Fetch the latest document before retrying.", currentRevision = revision });
    var next = revision + 1;
    await using var write = new NpgsqlCommand(@"INSERT INTO sync_documents(user_id,document_key,payload,revision,updated_at)
VALUES(@uid,@key,CAST(@payload AS jsonb),@revision,now())
ON CONFLICT(user_id,document_key) DO UPDATE SET payload=EXCLUDED.payload,revision=EXCLUDED.revision,updated_at=now()", conn, tx);
    write.Parameters.AddWithValue("uid", user.Id);
    write.Parameters.AddWithValue("key", key);
    write.Parameters.AddWithValue("payload", request.Payload);
    write.Parameters.AddWithValue("revision", next);
    await write.ExecuteNonQueryAsync(ct);
    await tx.CommitAsync(ct);
    return Results.Ok(new SyncWriteResponse(key, next, DateTimeOffset.UtcNow));
});

app.MapGet("/api/admin/summary", async (NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var user = await AuthenticateAsync(db, context, ct);
    if (user is null) return Results.Unauthorized();
    if (!string.Equals(user.Role, "admin", StringComparison.Ordinal)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("SELECT (SELECT count(*) FROM users WHERE disabled=false), (SELECT count(*) FROM sessions WHERE expires_at>now()), (SELECT count(*) FROM sync_documents), (SELECT count(*) FROM audit_events WHERE created_at>now()-interval '24 hours')", conn);
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    await reader.ReadAsync(ct);
    return Results.Ok(new { activeUsers = reader.GetInt64(0), activeSessions = reader.GetInt64(1), syncDocuments = reader.GetInt64(2), auditEventsLast24Hours = reader.GetInt64(3), capacityTarget = 1000, capacityVerified = false });
});

app.MapGet("/api/admin/users", async (NpgsqlDataSource db, HttpContext context, CancellationToken ct) =>
{
    var user = await AuthenticateAsync(db, context, ct);
    if (user is null) return Results.Unauthorized();
    if (!string.Equals(user.Role, "admin", StringComparison.Ordinal)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("SELECT id,email,role,disabled,created_at FROM users ORDER BY created_at DESC LIMIT 500", conn);
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    var users = new List<object>();
    while (await reader.ReadAsync(ct)) users.Add(new { id = reader.GetGuid(0), email = reader.GetString(1), role = reader.GetString(2), disabled = reader.GetBoolean(3), createdAt = reader.GetFieldValue<DateTimeOffset>(4) });
    return Results.Ok(users);
});

app.Run();

static async Task InitializeDatabaseAsync(NpgsqlDataSource db)
{
    await using var conn = await db.OpenConnectionAsync();
    var sql = """
    CREATE TABLE IF NOT EXISTS users (
      id uuid PRIMARY KEY, email text NOT NULL UNIQUE, password_hash bytea NOT NULL, password_salt bytea NOT NULL,
      role text NOT NULL DEFAULT 'user' CHECK (role IN ('user','admin')), disabled boolean NOT NULL DEFAULT false,
      created_at timestamptz NOT NULL DEFAULT now()
    );
    CREATE TABLE IF NOT EXISTS sessions (
      token_hash bytea PRIMARY KEY, user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
      expires_at timestamptz NOT NULL, created_at timestamptz NOT NULL DEFAULT now()
    );
    CREATE INDEX IF NOT EXISTS ix_sessions_user_expiry ON sessions(user_id, expires_at);
    CREATE TABLE IF NOT EXISTS sync_documents (
      user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE, document_key text NOT NULL,
      payload jsonb NOT NULL, revision bigint NOT NULL CHECK (revision > 0), updated_at timestamptz NOT NULL DEFAULT now(),
      PRIMARY KEY(user_id, document_key)
    );
    CREATE TABLE IF NOT EXISTS audit_events (
      id bigserial PRIMARY KEY, user_id uuid NULL REFERENCES users(id) ON DELETE SET NULL,
      event_type text NOT NULL, remote_address text NULL, created_at timestamptz NOT NULL DEFAULT now()
    );
    CREATE INDEX IF NOT EXISTS ix_audit_created ON audit_events(created_at DESC);
    DELETE FROM sessions WHERE expires_at <= now();
    """;
    await using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
}

static async Task BootstrapAdminAsync(NpgsqlDataSource db)
{
    var email = NormalizeEmail(Environment.GetEnvironmentVariable("NEXUS_ADMIN_EMAIL"));
    var password = Environment.GetEnvironmentVariable("NEXUS_ADMIN_PASSWORD");
    if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(password)) return;
    if (password.Length < 16) throw new InvalidOperationException("NEXUS_ADMIN_PASSWORD must be at least 16 characters.");
    await using var conn = await db.OpenConnectionAsync();
    await using var exists = new NpgsqlCommand("SELECT 1 FROM users WHERE email=@email", conn);
    exists.Parameters.AddWithValue("email", email);
    if (await exists.ExecuteScalarAsync() is not null) return;
    var salt = RandomNumberGenerator.GetBytes(16);
    await using var insert = new NpgsqlCommand("INSERT INTO users(id,email,password_hash,password_salt,role,created_at) VALUES(@id,@email,@hash,@salt,'admin',now())", conn);
    insert.Parameters.AddWithValue("id", Guid.NewGuid());
    insert.Parameters.AddWithValue("email", email);
    insert.Parameters.AddWithValue("hash", HashPassword(password, salt));
    insert.Parameters.AddWithValue("salt", salt);
    await insert.ExecuteNonQueryAsync();
}

static async Task<string> CreateSessionAsync(NpgsqlDataSource db, Guid userId, CancellationToken ct)
{
    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("INSERT INTO sessions(token_hash,user_id,expires_at) VALUES(@hash,@uid,now()+interval '7 days')", conn);
    cmd.Parameters.AddWithValue("hash", HashToken(token));
    cmd.Parameters.AddWithValue("uid", userId);
    await cmd.ExecuteNonQueryAsync(ct);
    return token;
}

static async Task<AuthenticatedUser?> AuthenticateAsync(NpgsqlDataSource db, HttpContext context, CancellationToken ct)
{
    var token = GetBearerToken(context);
    if (token is null) return null;
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("SELECT u.id,u.email,u.role FROM sessions s JOIN users u ON u.id=s.user_id WHERE s.token_hash=@hash AND s.expires_at>now() AND u.disabled=false", conn);
    cmd.Parameters.AddWithValue("hash", HashToken(token));
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct)) return null;
    return new AuthenticatedUser(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
}

static async Task WriteAuditAsync(NpgsqlDataSource db, Guid? userId, string eventType, string? remoteAddress, CancellationToken ct)
{
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("INSERT INTO audit_events(user_id,event_type,remote_address) VALUES(@uid,@type,@ip)", conn);
    cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("type", eventType);
    cmd.Parameters.AddWithValue("ip", (object?)remoteAddress ?? DBNull.Value);
    await cmd.ExecuteNonQueryAsync(ct);
}

static byte[] HashPassword(string password, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
static string? GetBearerToken(HttpContext context)
{
    var value = context.Request.Headers["Authorization"].ToString();
    return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && value.Length > 7 ? value[7..].Trim() : null;
}
static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
static bool IsValidEmail(string email) => email.Length is > 3 and <= 254 && email.Contains('@') && email.LastIndexOf('.') > email.IndexOf('@') + 1 && !email.Any(char.IsWhiteSpace);

record RegisterRequest(string? Email, string? Password);
record LoginRequest(string? Email, string? Password);
record SyncWriteRequest(string? Payload, long ExpectedRevision);
record UserDto(Guid Id, string Email, string Role);
record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
record SyncDocumentDto(string Key, string Payload, long Revision, DateTimeOffset UpdatedAt);
record SyncWriteResponse(string Key, long Revision, DateTimeOffset UpdatedAt);
record AuthenticatedUser(Guid Id, string Email, string Role);

