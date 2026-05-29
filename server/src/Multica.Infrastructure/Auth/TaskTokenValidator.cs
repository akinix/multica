using Microsoft.EntityFrameworkCore;
using Multica.Core.Auth;
using Multica.Core.Entities;
using Multica.Infrastructure.Data;

namespace Multica.Infrastructure.Auth;

/// <summary>
/// Validates task tokens (mat_) by database lookup.
/// Task tokens are single-use and short-lived, so no caching.
/// </summary>
public class TaskTokenValidator
{
    private readonly MulticaDbContext _db;

    public TaskTokenValidator(MulticaDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Validates a task token by its SHA-256 hash.
    /// Returns the identity if found and not expired, null otherwise.
    /// </summary>
    public async Task<TaskTokenIdentity?> ValidateAsync(string tokenHash)
    {
        var token = await _db.Set<TaskToken>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token is null)
            return null;

        if (token.ExpiresAt < DateTimeOffset.UtcNow)
            return null;

        return new TaskTokenIdentity
        {
            UserId = token.UserId,
            AgentId = token.AgentId,
            TaskId = token.TaskId,
            WorkspaceId = token.WorkspaceId
        };
    }
}
