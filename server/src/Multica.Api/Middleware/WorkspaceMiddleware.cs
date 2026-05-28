using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Api.Middleware;

/// <summary>
/// Workspace middleware for resolving workspace and validating membership.
/// Compatible with Go's workspace middleware implementation.
/// </summary>
public class WorkspaceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WorkspaceMiddleware> _logger;

    public WorkspaceMiddleware(RequestDelegate next, ILogger<WorkspaceMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, MulticaDbContext db)
    {
        // 1. Task token binding check
        if (context.Request.Headers["X-Actor-Source"] == "task_token")
        {
            var boundWorkspaceId = context.Request.Headers["X-Workspace-ID"].ToString();
            if (string.IsNullOrEmpty(boundWorkspaceId))
            {
                _logger.LogWarning("workspace: task token missing workspace binding");
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new { error = "workspace not found" });
                return;
            }

            // Verify workspace exists and user is a member
            if (!Guid.TryParse(boundWorkspaceId, out var wsId))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "invalid workspace_id" });
                return;
            }

            var userIdStr = context.Request.Headers["X-User-ID"].ToString();
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "user not authenticated" });
                return;
            }

            var member = await db.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserId == userId && m.WorkspaceId == wsId);

            if (member is null)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new { error = "workspace not found" });
                return;
            }

            context.Items["WorkspaceId"] = wsId;
            context.Items["Member"] = member;

            await _next(context);
            return;
        }

        // 2. Resolve workspace from slug or ID
        var workspaceId = await ResolveWorkspaceIdAsync(context.Request, db);
        if (workspaceId is null)
        {
            // Check if any workspace identifier was provided
            var hasSlug = !string.IsNullOrEmpty(context.Request.Headers["X-Workspace-Slug"].ToString()) ||
                         !string.IsNullOrEmpty(context.Request.Query["workspace_slug"]);
            var hasId = !string.IsNullOrEmpty(context.Request.Headers["X-Workspace-ID"].ToString()) ||
                       !string.IsNullOrEmpty(context.Request.Query["workspace_id"]);

            if (!hasSlug && !hasId)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "workspace_id or workspace_slug is required" });
                return;
            }

            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "workspace not found" });
            return;
        }

        // 3. Validate membership
        var userIdStr2 = context.Request.Headers["X-User-ID"].ToString();
        if (string.IsNullOrEmpty(userIdStr2) || !Guid.TryParse(userIdStr2, out var userId2))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "user not authenticated" });
            return;
        }

        var member2 = await db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId2 && m.WorkspaceId == workspaceId.Value);

        if (member2 is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "workspace not found" });
            return;
        }

        // 4. Inject into context
        context.Items["WorkspaceId"] = workspaceId.Value;
        context.Items["Member"] = member2;

        await _next(context);
    }

    private async Task<Guid?> ResolveWorkspaceIdAsync(HttpRequest request, MulticaDbContext db)
    {
        // Priority: X-Workspace-Slug header > ?workspace_slug query > X-Workspace-ID header > ?workspace_id query

        // 1. X-Workspace-Slug header
        var slug = request.Headers["X-Workspace-Slug"].ToString();
        if (!string.IsNullOrEmpty(slug))
        {
            var ws = await db.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Slug == slug);
            return ws?.Id;
        }

        // 2. ?workspace_slug query
        slug = request.Query["workspace_slug"];
        if (!string.IsNullOrEmpty(slug))
        {
            var ws = await db.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Slug == slug);
            return ws?.Id;
        }

        // 3. X-Workspace-ID header
        var idStr = request.Headers["X-Workspace-ID"].ToString();
        if (!string.IsNullOrEmpty(idStr) && Guid.TryParse(idStr, out var id))
        {
            return id;
        }

        // 4. ?workspace_id query
        idStr = request.Query["workspace_id"];
        if (!string.IsNullOrEmpty(idStr) && Guid.TryParse(idStr, out id))
        {
            return id;
        }

        return null;
    }
}

/// <summary>
/// Middleware for requiring workspace membership.
/// </summary>
public class RequireWorkspaceMemberMiddleware
{
    private readonly RequestDelegate _next;

    public RequireWorkspaceMemberMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Membership is already validated by WorkspaceMiddleware
        if (context.Items["Member"] is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "workspace membership required" });
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Middleware for requiring specific workspace roles.
/// </summary>
public class RequireWorkspaceRoleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _roles;

    public RequireWorkspaceRoleMiddleware(RequestDelegate next, params string[] roles)
    {
        _next = next;
        _roles = roles;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var member = context.Items["Member"] as Member;
        if (member is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "workspace membership required" });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(member.Role))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "insufficient permissions" });
            return;
        }

        await _next(context);
    }
}
