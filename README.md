# Synnotech.OracleCore
*Provides common functionality for database access to Oracle in .NET Core 3.1 / .NET 5 or higher.*

[![Synnotech Logo](synnotech-large-logo.png)](https://www.synnotech.de/)

[![License](https://img.shields.io/badge/License-MIT-green.svg?style=for-the-badge)](https://github.com/Synnotech-AG/Synnotech.OracleCore/blob/main/LICENSE)
[![NuGet](https://img.shields.io/badge/NuGet-1.0.0-blue.svg?style=for-the-badge)](https://www.nuget.org/packages/Synnotech.OracleCore/)

# How to Install

Synnotech.OracleCore is compiled against [.NET Standard 2.1](https://docs.microsoft.com/en-us/dotnet/standard/net-standard) and thus supports all corresponding platforms, e.g. .NET 6 and .NET Core 3.1.

Synnotech.OracleCore is available as a [NuGet package](https://www.nuget.org/packages/Synnotech.OracleCore/) and can be installed via:

- **Package Reference in csproj**: `<PackageReference Include="Synnotech.OracleCore" Version="1.0.0" />`
- **dotnet CLI**: `dotnet add package Synnotech.OracleCore`
- **Visual Studio Package Manager Console**: `Install-Package Synnotech.OracleCore`

# What does Synnotech.OracleCore offer you?

## Register an OracleConnection with the DI container

You can register an `OracleConnection` with the DI container by calling:

```csharp
services.AddOracleConnection(connectionString);
```

The connection string will normally be retrieved from an `IConfiguration` instance. You have the following additional options when calling `AddOracleConnection`:

- `connectionLifetime`: by default, the connection will be registered with a transient lifetime. In your app, it might also make sense to use a scoped lifetime.
- `registerFactoryDelegate`: a boolean value that indicates whether a `Func<OracleConnection>` will also be registered with the DI container as a singleton. You can inject this factory to any service that needs to resolve an `OracleConnection` dynamically. The default value is false. Keep in mind that your DI container might support these delegates out-of-the-box, e.g. LightInject provides a feature called [Function Factories](https://www.lightinject.net/#function-factories).

## Sessions with ADO.NET

Synnotech.OracleCore implements the `ReadOnlySession` and `Session` according to [Synnotech.DatabaseAbstractions](https://github.com/synnotech-AG/synnotech.DatabaseAbstractions). These allow you to make direct ADO.NET requests via a `OracleConnection` and `OracleCommand`.

### Read-only sessions

Consider the following abstraction which represents read-only database access to find a contact:

```csharp
public interface IGetContactSession : IDisposable
{
    Contact? GetContact(int id);
}
```

To implement this interface easily, you can derive from `ReadOnlySession`. A read-only session is a connection to a database that does not require a transaction by default (because it only loads data):

```csharp
public sealed class OracleGetContactSession : ReadOnlySession, IGetContactSession
{
    public OracleGetContactSession(OracleConnection connection) : base(connection) { }

    public Contact? GetContact(int id)
    {
        // The following line will create an OracleCommand and automatically
        // attaches the current transaction to it (if a transaction is present).
        using var command = CreateCommand(); 

        // We encourage you to use Light.EmbeddedResources and save your SQL
        // queries as embedded SQL files to your assembly.
        command.CommandText = SqlScripts.GetScript("GetContact.sql");
        command.Parameters.Add("pId", OracleDbType.Int32, ParameterDirection.Input).Value = id;

        using var reader = command.ExecuteReader();
        return DeserializeContact(reader);
    }

    private Contact? DeserializeContact(OracleDataReader reader)
    {
        if (!reader.HasRows)
            return null;

        var idOrdinal = reader.GetOrdinal(nameof(Contact.Id));
        var nameOrdinal = reader.GetOrdinal(nameof(Contact.Name));
        var emailOrdinal = reader.GetOrdinal(nameof(Contact.Email));

        if (!reader.Read())
            throw new SerializationException("The reader could not advance to the single row of the result");

        var id = reader.GetInt32(idOrdinal);
        var name = reader.GetString(nameOrdinal);
        var email = reader.GetString(emailOrdinal);
        return new Contact { Id = id, Name = name, Email = email };
    }
}
```

Your SQL script to get a person can be stored in a dedicated SQL file that can be embedded in your assembly. You can use [Light.EmbeddedResources](https://github.com/feO2x/Light.EmbeddedResources) to easily access them:

```csharp
public static class Sqlcripts
{
    public static string GetScript(string name) => typeof(Sqlcripts).GetEmbeddedResource(name);
}
```

```sql
SELECT *
FROM Contacts
WHERE Id = :pId;
```

You can then add your sessions to your DI container by calling the `AddSession` extension method:

```csharp
services.AddSession<IGetContactSession, OracleGetContactSession>();
```

Be sure that an `OracleConnection` is already registered with the DI Container. You can use the `AddOracleConnection` extension method for that.

To call your session, e.g. in an ASP.NET Core MVC controller, you simply instantiate the session via the factory:

```csharp
[ApiController]
[Route("api/contacts")]
public sealed class GetContactController : ControllerBase
{
    public GetContactController(Func<IGetContactSession> createSession) =>
        CreateSession = createSession;

    private Func<IGetContactSession> CreateSession { get; }

    [HttpGet("{id}")]
    public ActionResult<Contact> GetContact(int id)
    {
        // The following call will open the connection to the target database 
        var session = CreateSession();
        var contact = session.GetContact(id);
        if (contact == null)
            return NotFound();
        return contact;
    }
}
```

## Sessions that manipulate data

If you want to manipulate data, then simply derive from `Session` instead. This gives you an additional `SaveChanges` method that allows you to commit the internal transaction. By default, all classes deriving from `Session` will start a serializable transaction (this can be adjusted via an optional constructor parameter).

> Please be aware: Synnotech.DatabaseAbstractions does not support nested transactions. If you need them, you must create your own abstraction for it. However, we generally recommend to not use nested transactions, but use sequential transactions (e.g. when performing batch operations).

Consider the following abstraction:

```csharp
public interface IUpdateContactSession : ISession
{
    Contact? GetContact(int id);

    void UpdateContact(Contact contact);
}
```

This abstraction can be implemented by deriving from session:

```csharp
public sealed class OracleNewContactSession : Session, INewContactSession
{
    public OracleNewContactSession(OracleConnection connection) : base(connection) { }

    public void UpdateContact(Contact contact)
    {
        // The following line will create an OracleCommand and automatically
        // attaches the current transaction to it.
        using var command = CreateCommand();

        // We recommend to use Light.EmbeddedResources to retrieve embedded SQL scripts.
        // See the previous section about read-only sessions for more details.
        command.CommandText = SqlScripts.GetScript("UpdateContact.sql");
        command.Parameters.Add("pId", OracleDbType.Int32, ParameterDirection.Input).Value = contact.Id;
        command.Parameters.Add("pName", OracleDbType.NVarchar2, ParameterDirection.Input).Value = contact.Name;
        command.Parameters.Add("pEmail", OracleDbType.NVarchar2, ParameterDirection.Input).Value = contact.Email;

        command.ExecuteNonQuery();
    }

    // Other members are omitted, please check the read-only session section for the implementation
}
```

The DML statement of UpdateContact.sql might look like this:

```sql
UPDATE Contacts
SET Name = :pName, Email = :pEmail
WHERE Id = :pId;
```

To access your session in other classes, register it with the DI container:

```csharp
services.AddSession<IUpdateContactSession, OracleNewContactSession>();
```

You can then use it e.g. in an ASP.NET Core MVC controller:

```csharp
[ApiController]
[Route("/api/contacts/update")]
public sealed class UpdateContactController : ControllerBase
{
    public UpdateContactController(Func<IUpdateContactSession> createSession,
                                   IValidator<UpdateContactDto> validator,
                                   ILogger logger)
    {
        CreateSession = createSession;
        Validator = validator;
        Logger = logger;
    }

    private Func<IUpdateContactSession> CreateSession { get; }
    private IValidator<UpdateContactDto> Validator { get; }
    private ILogger Logger { get; }

    [HttpPut]
    public IActionResult UpdateContact(UpdateContactDto dto)
    {
        if (this.CheckForErrors(dto, validator, out var badResult))
            return badResult;
        
        using var session = CreateSession();
        var contact = session.GetContact(dto.Id);
        if (contact == null)
            return NotFound();
        
        contact.Name = dto.Name;
        contact.Email = dto.Email;
        session.UpdateContact(contact);
        session.SaveChanges();
        Logger.Information("Contact {@Contact} was updated successfully", contact);
        return NoContent();
    }
}
```