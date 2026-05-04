using FlipKit.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlipKit.Core.Tests.Infrastructure;

/// <summary>
/// Per-test SQLite in-memory database with the FlipKit schema applied. Wraps the
/// `:memory:` connection lifecycle — disposing this disposes the DbContext, the
/// ServiceProvider, and the underlying connection (which is what the in-memory
/// database lives on, so closing it discards the data).
///
/// Two access patterns:
/// - <see cref="Context"/> — direct DbContext for repository-style tests.
/// - <see cref="ServiceProvider"/> — DI container for services that resolve
///   FlipKitDbContext via IServiceProvider (e.g. ChecklistLearningService creates
///   its own scopes).
///
/// Use one or the other in a given test — sharing the same connection between a
/// directly-held context and DI-resolved contexts is fine for SQLite, but mixing
/// access patterns in one test makes failures harder to localize.
/// </summary>
public sealed class TestDbContext : IDisposable
{
    public FlipKitDbContext Context { get; }
    public IServiceProvider ServiceProvider { get; }

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private TestDbContext(SqliteConnection connection, ServiceProvider provider, FlipKitDbContext context)
    {
        _connection = connection;
        _provider = provider;
        ServiceProvider = provider;
        Context = context;
    }

    public static TestDbContext Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<FlipKitDbContext>(opts => opts.UseSqlite(connection));
        var provider = services.BuildServiceProvider();

        // Initialize schema. Direct context for the .Context surface.
        var options = new DbContextOptionsBuilder<FlipKitDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new FlipKitDbContext(options);
        context.Database.EnsureCreated();

        return new TestDbContext(connection, provider, context);
    }

    public void Dispose()
    {
        Context.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }
}
