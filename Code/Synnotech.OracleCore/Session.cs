using System;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using Synnotech.DatabaseAbstractions;

namespace Synnotech.OracleCore;

/// <summary>
/// Represents a session to an Oracle database that can read as well as
/// manipulate data. A transaction is automatically started in the constructor.
/// </summary>
public abstract class Session : ReadOnlySession, ISession
{
    /// <summary>
    /// Initializes a new instance of <see cref="Session" />.
    /// </summary>
    /// <param name="connection">The connection to the oracle server.</param>
    /// <param name="transactionLevel">
    /// The isolation level for the transaction (optional). The default value is <see cref="IsolationLevel.Serializable" />.
    /// When this value is set to <see cref="IsolationLevel.Unspecified" />, no transaction will be started.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> is null.</exception>
    protected Session(OracleConnection connection, IsolationLevel transactionLevel = IsolationLevel.Serializable)
        : base(connection, transactionLevel) { }

    /// <summary>
    /// Commits the underlying transaction (if there is any, this depends on the transactionLevel parameter in your constructor).
    /// </summary>
    public void SaveChanges() => Transaction?.Commit();
}