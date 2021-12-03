using System;
using System.Data;
using Light.GuardClauses;
using Oracle.ManagedDataAccess.Client;

namespace Synnotech.OracleCore;

/// <summary>
/// Represents a session to an Oracle database that only reads data and
/// thus requires no transaction. An optional transaction can be started
/// via the second constructor parameter.
/// </summary>
public abstract class ReadOnlySession : IDisposable
{
    /// <summary>
    /// Initializes a new instance of <see cref="ReadOnlySession" />.
    /// </summary>
    /// <param name="connection">The connection to the oracle server.</param>
    /// <param name="transactionLevel">
    /// The isolation level for the transaction (optional). The default value is <see cref="IsolationLevel.Unspecified" />.
    /// When this value is set to <see cref="IsolationLevel.Unspecified" />, no transaction will be started.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection" /> is null.</exception>
    protected ReadOnlySession(OracleConnection connection,
                              IsolationLevel transactionLevel = IsolationLevel.Unspecified)
    {
        Connection = connection.MustNotBeNull(nameof(connection));
        connection.Open();

        if (transactionLevel != IsolationLevel.Unspecified)
            Transaction = connection.BeginTransaction(transactionLevel);
    }

    /// <summary>
    /// Gets the connection to the oracle server.
    /// </summary>
    protected OracleConnection Connection { get; }

    /// <summary>
    /// Gets the transaction that was started on this transaction.
    /// </summary>
    protected OracleTransaction? Transaction { get; }

    /// <summary>
    /// Creates a new oracle command from the connection and automatically
    /// attaches it to the transaction if there is one. You must dispose
    /// the command by yourself.
    /// </summary>
    protected OracleCommand CreateCommand()
    {
        var command = Connection.CreateCommand();
        if (Transaction != null)
            command.Transaction = Transaction;
        return command;
    }

    /// <summary>
    /// Disposes the internal transaction (if there is one) and the oracle connection.
    /// </summary>
    public void Dispose()
    {
        Transaction?.Dispose();
        Connection.Dispose();
    }
}