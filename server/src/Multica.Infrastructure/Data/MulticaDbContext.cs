using Microsoft.EntityFrameworkCore;
using Multica.Core.Entities;

namespace Multica.Infrastructure.Data;

/// <summary>
/// Application DbContext for Multica.
/// Compatible with Go's sqlc-generated queries.
/// </summary>
public class MulticaDbContext : DbContext
{
    public MulticaDbContext(DbContextOptions<MulticaDbContext> options) : base(options)
    {
    }

    // Entity sets
    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<TaskToken> TaskTokens => Set<TaskToken>();
    public DbSet<DaemonToken> DaemonTokens => Set<DaemonToken>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();
    public DbSet<IssueToLabel> IssueToLabels => Set<IssueToLabel>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<IssuePullRequest> IssuePullRequests => Set<IssuePullRequest>();
    public DbSet<GithubPullRequest> GithubPullRequests => Set<GithubPullRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MulticaDbContext).Assembly);
    }
}
