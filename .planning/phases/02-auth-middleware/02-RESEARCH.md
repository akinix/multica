# Phase 2: Auth & Middleware — Research

**Researched:** 2026-05-28
**Status:** Complete

## Go → C# Component Mapping

### Auth Components

| Go File | C# Equivalent | Approach |
|---------|--------------|----------|
| `auth/jwt.go` | `System.IdentityModel.Tokens.Jwt` | Built-in ASP.NET Core JWT Bearer handler |
| `auth/cookie.go` | Custom middleware | ASP.NET Core cookie auth + custom CSRF |
| `auth/pat_cache.go` | `StackExchange.Redis` IDatabase | Direct port, same Redis key scheme |
| `auth/daemon_token_cache.go` | `StackExchange.Redis` IDatabase | Direct port, same Redis key scheme |
| `auth/membership_cache.go` | `StackExchange.Redis` IDatabase | Direct port, same Redis key scheme |
| `auth/cloud_pat.go` | `HttpClient` + Redis cache | Custom service, same Fleet API contract |
| `auth/cloudfront.go` | `AWSSDK.CloudFront` + `AWSSDK.SecretsManager` | RSA-SHA1 signing via BouncyCastle or built-in |

### Middleware Components

| Go File | C# Equivalent | Approach |
|---------|--------------|----------|
| `middleware/auth.go` | Custom middleware + `IAuthenticationHandler` | Prefix-based token dispatch |
| `middleware/daemon_auth.go` | Custom middleware | Parallel to auth middleware |
| `middleware/workspace.go` | Custom middleware | EF Core membership lookup + context injection |
| `middleware/ratelimit.go` | Custom middleware + Redis Lua script | Same sliding-window algorithm |
| `middleware/client.go` | Custom middleware | Simple header extraction |
| `middleware/request_logger.go` | Serilog enricher | Extend existing Serilog pipeline |
| `middleware/csp.go` | Custom middleware | Static header injection |

## Implementation Details

### 1. JWT Authentication

**Library:** `Microsoft.AspNetCore.Authentication.JwtBearer` (built-in)

**Key mapping:**
```csharp
// Go: jwt.Parse(tokenString, keyFunc)
// C#: services.AddAuthentication().AddJwtBearer(options => { ... })

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtSecret),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });
```

**Go JWT claims:** `sub` (user ID), `email`
**C# JWT claims:** Same — `ClaimTypes.NameIdentifier` for sub, `ClaimTypes.Email` for email

**Token generation:**
```csharp
// Go: jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
// C#: new JwtSecurityTokenHandler().WriteToken(tokenDescriptor)
```

### 2. Token Prefix Dispatch

**Go pattern:** Single auth middleware checks `strings.HasPrefix(tokenString, "mul_")` etc.
**C# approach:** Custom `IAuthenticationHandler` that dispatches based on prefix.

**Recommended architecture:**
```csharp
// Register multiple authentication schemes
builder.Services.AddAuthentication()
    .AddJwtBearer("Jwt", ...)           // Bearer tokens
    .AddScheme<PatAuthHandler>("PAT", ...)  // mul_ prefix
    .AddScheme<DaemonAuthHandler>("Daemon", ...)  // mdt_ prefix
    .AddScheme<TaskAuthHandler>("Task", ...)  // mat_ prefix
    .AddScheme<CloudPatAuthHandler>("CloudPAT", ...)  // mcn_ prefix
    .AddPolicyScheme("MulticaAuth", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var auth = context.Request.Headers.Authorization.ToString();
            if (auth.StartsWith("mul_")) return "PAT";
            if (auth.StartsWith("mdt_")) return "Daemon";
            if (auth.StartsWith("mat_")) return "Task";
            if (auth.StartsWith("mcn_")) return "CloudPAT";
            return "Jwt"; // Default: Bearer JWT
        }
    });
```

**Or simpler:** Single middleware that handles all prefixes (closer to Go pattern, fewer abstractions).

**Decision:** Use the simpler single-middleware approach to match Go 1:1. The `PolicyScheme` approach adds unnecessary abstraction for a migration.

### 3. Cookie Auth + CSRF

**Go cookie names:** `multica_auth` (HttpOnly), `multica_csrf` (readable)
**C# implementation:**

```csharp
// Set cookies on login
Response.Cookies.Append("multica_auth", token, new CookieOptions
{
    HttpOnly = true,
    Secure = isSecureCookie,
    SameSite = SameSiteMode.Strict,
    Domain = cookieDomain,
    MaxAge = authTokenTTL
});

// CSRF validation: HMAC-SHA256(nonce, authToken)
// Go: generateCSRFToken(authToken) → nonce.hex + "." + hmac.hex
// C#: Same algorithm, using System.Security.Cryptography.HMACSHA256
```

**CSRF validation logic:**
1. Skip for GET/HEAD/OPTIONS
2. Read `X-CSRF-Token` header
3. Read `multica_auth` cookie
4. Split token on "." → nonce + signature
5. Verify HMAC-SHA256(nonce, cookieValue) == signature

### 4. PAT Token Cache

**Go:** `PATCache` struct with Redis Get/Set/Invalidate
**C#:** Service class wrapping `IDatabase`

```csharp
public class PatCache
{
    private const string Prefix = "mul:auth:pat:";
    private static readonly TimeSpan DefaultTTL = TimeSpan.FromMinutes(10);

    private readonly IDatabase _redis;

    public PatCache(IDatabase redis) => _redis = redis;

    public async Task<string?> GetAsync(string hash)
    {
        var value = await _redis.StringGetAsync(Prefix + hash);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task SetAsync(string hash, string userId, TimeSpan? ttl = null)
    {
        await _redis.StringSetAsync(Prefix + hash, userId, ttl ?? DefaultTTL);
    }

    public async Task InvalidateAsync(string hash)
    {
        await _redis.KeyDeleteAsync(Prefix + hash);
    }
}
```

**Same pattern for DaemonTokenCache and MembershipCache.**

### 5. Google OAuth

**Go:** Manual `HttpClient` POST to Google token endpoint
**C#:** Same approach — `HttpClient` to Google APIs

**Endpoints:**
- Token exchange: `POST https://oauth2.googleapis.com/token`
- User info: `GET https://www.googleapis.com/oauth2/v2/userinfo`

**Flow:**
1. Frontend popup → Google authorization code
2. Frontend POST code to `/api/auth/google/callback`
3. Backend exchanges code for access_token via HttpClient
4. Backend fetches user info (email, name, picture, sub)
5. Backend creates/updates user, generates JWT, sets cookies

**No `Microsoft.AspNetCore.Authentication.Google`** — per CONTEXT.md decision D-07.

### 6. CloudFront Signing

**Go:** RSA-SHA1 signing with AWS Secrets Manager key
**C# options:**

**Option A: AWSSDK.SecretsManager + System.Security.Cryptography.RSA**
```csharp
// Load key from Secrets Manager
var secret = await secretsClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });
var rsa = RSA.Create();
rsa.ImportFromPem(secret.SecretString.AsSpan());

// Sign policy
var policy = $"{{\"Statement\":[{{\"Resource\":\"https://{domain}/*\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{expiryEpoch}}}}}}}]}}";
var signature = rsa.SignData(Encoding.UTF8.GetBytes(policy), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
```

**Option B: BouncyCastle** — more control but adds dependency.

**Decision:** Use built-in `System.Security.Cryptography.RSA` — .NET 9 supports PKCS1 signing natively. Only fall back to BouncyCastle if PKCS1/SHA1 combination isn't supported.

**CloudFront cookie names:** `CloudFront-Policy`, `CloudFront-Signature`, `CloudFront-Key-Pair-Id`

### 7. Rate Limiting

**Go:** Redis Lua script with INCR + EXPIRE (atomic)
**C#:** Same Lua script via `StackExchange.Redis`

```csharp
private const string RateLimitScript = @"
local count = redis.call('INCR', KEYS[1])
if count == 1 then
    redis.call('EXPIRE', KEYS[1], ARGV[1])
end
return count
";

private static readonly LuaScript Script = LuaScript.Prepare(RateLimitScript);

public async Task<bool> IsRateLimitedAsync(string path, string ip, int limit, TimeSpan window)
{
    var key = $"mul:ratelimit:{path.TrimStart('/').Replace('/', ':')}:{ip}";
    var result = await _redis.ScriptEvaluateAsync(Script, new { key = key, window = (int)window.TotalSeconds });
    return (long)result > limit;
}
```

**IP extraction:** Same trusted-proxy logic — check `X-Forwarded-For` only when RemoteAddr is in trusted CIDRs.

### 8. Workspace Middleware

**Go:** `RequireWorkspaceMember` resolves workspace from slug/UUID, checks membership
**C#:** Custom middleware with same priority order:
1. Task token binding (X-Actor-Source == "task_token")
2. X-Workspace-Slug header → EF Core query
3. ?workspace_slug query → EF Core query
4. X-Workspace-ID header
5. ?workspace_id query

**Context injection:** Use `HttpContext.Items` or custom `IHttpContextAccessor` pattern.

### 9. Client Metadata

**Go:** Extract X-Client-Platform/Version/OS from headers
**C#:** Simple middleware, store in `HttpContext.Items`

### 10. Request Logging

**Go:** Custom slog handler with slow-request detection
**C#:** Extend existing Serilog enricher in Program.cs

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("ClientPlatform", httpContext.Request.Headers["X-Client-Platform"].ToString());
        // Add: request_id, user_id, client_version, client_os
    };
});
```

### 11. CSP Header

**Go:** Static CSP string
**C#:** Simple middleware, same static string

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy", cspValue);
    await next();
});
```

## Middleware Pipeline Order

**Go order:** RequestID → ClientMetadata → RequestLogger → HTTPMetrics → Recoverer → CSP → CORS → Auth → Workspace

**C# order:**
```csharp
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ClientMetadataMiddleware>();
app.UseSerilogRequestLogging(...);
// HTTPMetrics via prometheus-net middleware
app.UseExceptionHandler(...);
app.UseMiddleware<CspMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// Workspace middleware applied per-route via RequireWorkspaceMember()
```

## Key Gotchas

### 1. Cookie Domain Handling
- Go: `cookieDomain()` checks for IP addresses and warns
- C#: Same logic needed — `IPAddress.TryParse()` to detect IP domains

### 2. Secure Cookie Detection
- Go: Parse `FRONTEND_ORIGIN` URL, check if scheme is https
- C#: Same — `new Uri(frontendOrigin).Scheme == "https"`

### 3. Token Hashing
- Go: `sha256.Sum256([]byte(token))` → hex string
- C#: `SHA256.HashData(Encoding.UTF8.GetBytes(token))` → `Convert.ToHexString()`

### 4. CSRF Token Format
- Go: `hex(nonce) + "." + hex(HMAC-SHA256(nonce, authToken))`
- C#: Same format — must be compatible with existing frontend

### 5. UUID Handling
- Go: `pgtype.UUID` with custom `UUIDToString`
- C#: `Guid` type — EF Core maps `uuid` columns to `Guid` natively

### 6. Cloud PAT Error Mapping
- Go: `ErrCloudPATInvalid` → 401, `ErrCloudPATUnavailable` → 503
- C#: Custom exception types with same HTTP status mapping

### 7. Trusted Proxy CIDR Parsing
- Go: `net.ParseCIDR()` for X-Forwarded-For trust
- C#: `System.Net.IPAddress.Parse()` + `System.Net.IPNetwork.Parse()`

## Dependencies to Add

| Package | Purpose | Version |
|---------|---------|---------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT validation | .NET 9 built-in |
| `AWSSDK.SecretsManager` | CloudFront key loading | latest |
| `AWSSDK.CloudFront` | (optional) CloudFront signing helpers | latest |
| `prometheus-net.AspNetCore` | HTTP metrics | latest |

## Validation Architecture

**Testable units:**
1. Token prefix dispatch → unit test with mock HttpContext
2. CSRF validation → unit test with known token pairs
3. Redis cache Get/Set/Invalidate → integration test with Testcontainers
4. Rate limiting → integration test with Redis
5. Google OAuth → unit test with mock HttpClient
6. CloudFront signing → unit test with known key/policy/signature

**Integration test strategy:**
- Use Testcontainers for PostgreSQL and Redis
- Test full auth flow: login → JWT → authenticated request
- Test PAT auth flow: create PAT → authenticate with token
- Test workspace middleware: resolve slug → check membership

---

*Phase: 2-Auth & Middleware*
*Researched: 2026-05-28*
