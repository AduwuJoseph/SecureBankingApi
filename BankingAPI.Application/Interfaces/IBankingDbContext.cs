using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BankingAPI.Domain.Entities;

namespace BankingAPI.Application.Interfaces;

/// <summary>
/// Application database context interface for abstraction and testing
/// </summary>
public interface IBankingDbContext
{
    /// <summary>
    /// Users DbSet
    /// </summary>
    DbSet<User> Users { get; }

    /// <summary>
    /// Accounts DbSet
    /// </summary>
    DbSet<Account> Accounts { get; }

    /// <summary>
    /// Account Ledger DbSet
    /// </summary>
    DbSet<AccountLedger> AccountLedgers { get; }

    /// <summary>
    /// Transactions DbSet
    /// </summary>
    DbSet<Transaction> Transactions { get; }

    /// <summary>
    /// Idempotency Logs DbSet
    /// </summary>
    DbSet<IdempotencyLog> IdempotencyLogs { get; }

    /// <summary>
    /// Refresh Tokens DbSet
    /// </summary>
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>
    /// Saves all changes made in this context to the database
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of state entries written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Database transaction</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads an entity from the database, overwriting any property values
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="entity">Entity to reload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReloadEntityAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets the current row version for an account
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Row version byte array</returns>
    Task<byte[]> GetCurrentRowVersionAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a raw SQL query and returns the results
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    /// <param name="sql">SQL query</param>
    /// <param name="parameters">SQL parameters</param>
    /// <returns>Query results</returns>
    IQueryable<T> FromSqlRaw<T>(string sql, params object[] parameters) where T : class;

    /// <summary>
    /// Sets the entity state
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="entity">Entity</param>
    /// <param name="state">Entity state</param>
    void SetEntityState<T>(T entity, EntityState state) where T : class;

    /// <summary>
    /// Gets the entity entry for tracking
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="entity">Entity</param>
    /// <returns>Entity entry</returns>
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> Entry<T>(T entity) where T : class;
}