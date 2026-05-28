---
phase: 2
plan: 02
subsystem: auth
tags: [auth, cookie, csrf, oauth, cloudfront]
requires: [AUTH-06, AUTH-07, AUTH-08]
provides: [cookie-service, csrf-validator, google-oauth, cloudfront-signer, auth-endpoints]
tech-stack:
  added: []
  patterns: [cookie-auth, csrf-protection, oauth2-code-flow]
key-files:
  created:
    - server/src/Multica.Core/Auth/CookieService.cs
    - server/src/Multica.Core/Auth/CsrfValidator.cs
    - server/src/Multica.Core/Auth/GoogleOAuthService.cs
    - server/src/Multica.Core/Auth/CloudFrontSigner.cs
    - server/src/Multica.Api/Handlers/AuthHandler.cs
  modified:
    - server/src/Multica.Api/Program.cs
    - server/src/Multica.Api/Multica.Api.csproj
key-decisions:
  - Cookie names match Go: multica_auth (HttpOnly), multica_csrf (readable)
  - CSRF token format: hex(nonce).hex(HMAC-SHA256(nonce, authToken))
  - Google OAuth uses manual HttpClient implementation (not built-in middleware)
  - CloudFront uses RSA-SHA1 signing with CloudFront base64 encoding
requirements-completed: [AUTH-06, AUTH-07, AUTH-08]
duration: 20 min
completed: 2026-05-28
---

# Phase 2 Plan 02: Cookie Auth, CSRF, Google OAuth & CloudFront Summary

Implemented cookie-based authentication with CSRF protection, Google OAuth login flow, and CloudFront signed cookie/URL generation.

## What Was Built

### CookieService
- Constants: `multica_auth` (HttpOnly), `multica_csrf` (readable)
- SetAuthCookies: sets both cookies with configurable domain, TTL, Secure flag
- ClearAuthCookies: expires both cookies
- IsSecureCookie: derives from FRONTEND_ORIGIN scheme
- CookieDomain: validates against IP addresses (RFC 6265)

### CsrfValidator
- Format: hex(nonce).hex(HMAC-SHA256(nonce, authToken))
- Validation skips safe methods (GET, HEAD, OPTIONS)
- Timing-safe comparison via CryptographicOperations.FixedTimeEquals

### GoogleOAuthService
- ExchangeCodeAsync: POST https://oauth2.googleapis.com/token
- GetUserInfoAsync: GET https://www.googleapis.com/oauth2/v2/userinfo
- Returns GoogleUserInfo with email, name, picture, sub

### CloudFrontSigner
- RSA-SHA1 signing for CloudFront private distributions
- Signed cookies: CloudFront-Policy, CloudFront-Signature, CloudFront-Key-Pair-Id
- Signed URLs with query parameters
- CloudFront base64 encoding: + → -, = → _, / → ~
- Returns null when CLOUDFRONT_KEY_PAIR_ID not configured

### Auth API Endpoints
- POST /api/auth/google/callback: Google OAuth flow
- POST /api/auth/logout: clear cookies
- GET /api/auth/me: current user info from X-User-ID header

### DI Registration
- CookieService, GoogleOAuthService, JwtTokenService: singleton
- Auth endpoints registered via MapAuthEndpoints()

## Deviations from Plan

None — plan executed exactly as written.

## Verification Results

1. ✅ Cookie names and attributes match Go exactly
2. ✅ CSRF token format and validation match Go exactly
3. ✅ Google OAuth flow: code → token → user info → JWT → cookies
4. ✅ CloudFront signed cookies implementation complete
5. ✅ Auth endpoints return correct JSON shapes
6. ✅ Build succeeds for all projects

## Self-Check: PASSED

All tasks completed, all verifications passed, SUMMARY.md created.

## Next Steps

Ready for Plan 03: Auth Middleware Pipeline & Workspace Enforcement
