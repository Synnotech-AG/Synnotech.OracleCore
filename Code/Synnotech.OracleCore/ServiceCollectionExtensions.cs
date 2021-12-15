using System;
using Light.GuardClauses;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;
using Synnotech.Core.DependencyInjection;

namespace Synnotech.OracleCore;

/// <summary>
/// Provides members to add <see cref="OracleConnection" /> and sessions
/// to the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="OracleConnection" /> with the DI container. The connection
    /// will be created with a transient lifetime by default. The specified connection string
    /// will be passed to its constructor upon creation. An optional Func&lt;OracleConnection&gt;
    /// can also be registered to dynamically resolve connections via this singleton delegate,
    /// but this is turned off by default.
    /// </summary>
    /// <param name="services">The collection that represent the registered services of the DI container.</param>
    /// <param name="connectionString">The connection string that will be passed to the Oracle connection.</param>
    /// <param name="connectionLifetime">The lifetime of the connection. By default, it is transient.</param>
    /// <param name="registerFactoryDelegate">
    /// The boolean value indicating whether a Func&lt;OracleConnection&gt; should also be registered with the
    /// DI container as a singleton. You can inject this delegate into your classes to dynamically resolve Oracle connections.
    /// The default value is false.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is null.</exception>
    public static IServiceCollection AddOracleConnection(this IServiceCollection services,
                                                         string connectionString,
                                                         ServiceLifetime connectionLifetime = ServiceLifetime.Transient,
                                                         bool registerFactoryDelegate = false)
    {
        services.MustNotBeNull();

        services.Add(new ServiceDescriptor(typeof(OracleConnection), _ => new OracleConnection(connectionString), connectionLifetime));

        if (registerFactoryDelegate)
            services.AddSingleton<Func<OracleConnection>>(container => container.GetRequiredService<OracleConnection>);

        return services;
    }

    /// <summary>
    /// Registers a session abstraction and its implementation with the DI container. The session will have
    /// a transient lifetime by default. Also, a Func&lt;TSessionAbstraction&gt; is registered by default
    /// as a singleton. You can inject this delegate into your classes to dynamically resolve your session
    /// by calling the delegate.
    /// </summary>
    /// <param name="services">The collection that represent the registered services of the DI container.</param>
    /// <param name="sessionLifetime">The lifetime of the session. By default, it is transient.</param>
    /// <param name="registerFactoryDelegate">
    /// The boolean value indicating whether a Func&lt;TSessionAbstraction&gt; should also be registered with the
    /// DI container as a singleton. You can inject this delegate into your classes to dynamically resolve your session.
    /// The default value depends on <see cref="ContainerSettingsContext" />.
    /// </param>
    /// <typeparam name="TSessionAbstraction">The interface of abstract base class that you use as an abstraction for Oracle database access.</typeparam>
    /// <typeparam name="TSessionImplementation">The class that implements your abstraction.</typeparam>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddSession<TSessionAbstraction, TSessionImplementation>(this IServiceCollection services,
                                                                                             ServiceLifetime sessionLifetime = ServiceLifetime.Transient,
                                                                                             bool? registerFactoryDelegate = null)
        where TSessionAbstraction : IDisposable
        where TSessionImplementation : TSessionAbstraction
    {
        services.MustNotBeNull();

        services.Add(new ServiceDescriptor(typeof(TSessionAbstraction), typeof(TSessionImplementation), sessionLifetime));
        if (ContainerSettingsContext.Settings.CheckIfFactoryDelegateShouldBeRegistered(registerFactoryDelegate))
            services.AddSingleton<Func<TSessionAbstraction>>(container => container.GetRequiredService<TSessionAbstraction>);
        return services;
    }
}