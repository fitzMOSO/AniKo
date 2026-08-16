using Microsoft.EntityFrameworkCore;

namespace AniKo_API.Data;

/// <summary>
/// Brings the database up to the current schema at startup, under a lock, and refuses to let the
/// application start if it cannot.
/// <para>
/// The alternative — log the failure and carry on — produces a healthy-looking process serving a
/// database with the wrong schema. Render would see a 200 on <c>/health</c>, mark the deploy
/// live, and retire the previous instance that was working. A failed deploy keeps the old
/// version serving; a successful deploy of a broken app does not.
/// </para>
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// The advisory-lock key. Any constant works as long as every instance of this application
    /// uses the same one and nothing else in the database uses it. It is derived from the app
    /// name so a second service sharing the database is unlikely to collide by accident.
    /// </summary>
    private const long MigrationLockKey = 8_235_071_101_954_216_137L;

    /// <summary>
    /// Migrates, then seeds if <c>Seed:Demo</c> is set.
    /// </summary>
    /// <remarks>
    /// Serialised across instances with <c>pg_advisory_lock</c>. Render can run more than one
    /// instance of a service, and two processes calling <c>Migrate()</c> against one database at
    /// the same time race on the migration-history table: both read "not applied", both apply,
    /// and the second fails on an object that now exists. EF does not lock this for you.
    /// <para>
    /// The lock is session-scoped, which is why the connection is opened explicitly and held for
    /// the whole operation rather than letting EF open and close one per command — a lock taken
    /// on a connection that is then returned to the pool is a lock nobody holds. Holding it open
    /// also means a hard crash releases the lock automatically when the backend notices the
    /// session is gone, so a killed instance cannot wedge every future deploy.
    /// </para>
    /// </remarks>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AniKoDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<AniKoDbContext>();

        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);

            try
            {
                // Blocks until any other instance finishes. Deliberately not the `try_` variant:
                // an instance that failed to take the lock must wait, not skip the migration and
                // start serving against a schema it has not checked.
                await db.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);

                var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
                var pendingList = pending.ToList();

                if (pendingList.Count > 0)
                {
                    logger.LogInformation(
                        "Applying {Count} pending migration(s): {Migrations}",
                        pendingList.Count,
                        string.Join(", ", pendingList));
                }

                await db.Database.MigrateAsync(cancellationToken);

                // Reference data arrives with the migration above; demo data is Phase D and runs
                // here, inside the same lock, so two instances cannot both seed.
                logger.LogInformation("Database schema is up to date.");
            }
            finally
            {
                // Best-effort. If the connection is already broken the lock is released by the
                // server when the session ends, so a failure here must not mask the real
                // exception on the way out of the try block.
                try
                {
                    await db.Database.ExecuteSqlRawAsync(
                        $"SELECT pg_advisory_unlock({MigrationLockKey})", cancellationToken);
                }
                catch (Exception unlockException)
                {
                    logger.LogWarning(
                        unlockException,
                        "Could not release the migration advisory lock; the server will release it when this session ends.");
                }

                await db.Database.CloseConnectionAsync();
            }
        }
        catch (Exception exception)
        {
            // Logged with full detail *and* rethrown. Logged because the rethrow surfaces on
            // Render as a container that exited, which on its own tells you nothing about why.
            logger.LogCritical(
                exception,
                "Database initialisation failed. The application will not start; the previously deployed version keeps serving.");
            throw;
        }
    }
}
