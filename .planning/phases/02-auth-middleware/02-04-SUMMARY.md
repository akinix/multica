---
phase: 2
plan: 04
subsystem: middleware
tags: [middleware, metrics, logging, csp]
requires: [MW-01, MW-02, MW-03, MW-04, MW-05, MW-06, MW-07]
provides: [request-id, client-metadata, csp, http-metrics, enhanced-logging, exception-handler]
tech-stack:
  added: [prometheus-net.AspNetCore]
  patterns: [serilog-enrichment, slow-request-detection, webhook-redaction]
key-files:
  created:
    - server/src/Multica.Api/Middleware/RequestIdMiddleware.cs
    - server/src/Multica.Api/Middleware/ClientMetadataMiddleware.cs
    - server/src/Multica.Api/Middleware/CspMiddleware.cs
  modified:
    - server/src/Multica.Api/Program.cs
    - server/src/Multica.Api/Multica.Api.csproj
key-decisions:
  - Request ID uses GUID v7 for uniqueness
  - Client metadata stored in HttpContext.Items and Activity.Current for distributed tracing
  - CSP header value matches Go exactly
  - HTTP metrics via prometheus-net with /metrics endpoint
  - Slow request detection: >1s Warning, >5s Error
  - Webhook path redaction for /api/webhooks/autopilots/
requirements-completed: [MW-01, MW-02, MW-03, MW-04, MW-05, MW-06, MW-07]
duration: 10 min
completed: 2026-05-28
---

# Phase 2 Plan 04: General Middleware Pipeline Summary

Implemented the remaining middleware components — request ID, client metadata, CSP, HTTP metrics, and enhanced request logging.

## What Was Built

### RequestIdMiddleware
- Generates GUID v7 for each request
- Sets X-Request-Id response header
- Preserves existing X-Request-Id from trusted proxies
- Stores in HttpContext.Items for downstream use

### ClientMetadataMiddleware
- Extracts headers: X-Client-Platform, X-Client-Version, X-Client-OS
- Stores in HttpContext.Items
- Sets on Activity.Current for distributed tracing
- Extension method: GetClientMetadata() returns (platform, version, os)

### CspMiddleware
- Sets Content-Security-Policy header on every response
- CSP value matches Go exactly: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https: data:; connect-src 'self' wss:; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'

### HTTP Metrics (prometheus-net)
- Added prometheus-net.AspNetCore package
- UseHttpMetrics() middleware for request metrics
- MapMetrics() endpoint at /metrics
- Metrics: http_requests_total, http_request_duration_seconds, http_requests_in_progress

### Enhanced Request Logging
- Added ClientVersion, ClientOS, UserId enrichment
- Webhook path redaction for /api/webhooks/autopilots/
- Slow request detection: >1s Warning, >5s Error
- Health endpoint skip
- Error enrichment for 500+ status codes

### Panic/Exception Recovery
- ExceptionHandler returns 500 with application/problem+json
- Detail only included in Development mode
- Error responses logged at Error level

## Deviations from Plan

None — plan executed exactly as written.

## Verification Results

1. ✅ X-Request-Id present on all responses
2. ✅ Client metadata headers extracted and available
3. ✅ CSP header matches Go exactly
4. ✅ /metrics endpoint returns Prometheus format
5. ✅ Request logs include all required fields
6. ✅ Slow requests detected and logged
7. ✅ Webhook paths redacted in logs
8. ✅ Exception handler returns correct format
9. ✅ Build succeeds
10. ✅ dotnet test passes

## Self-Check: PASSED

All tasks completed, all verifications passed, SUMMARY.md created.

## Next Steps

Phase 2 complete — ready for Phase 3: Issues & Comments
