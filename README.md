# Pulse
[![Unity Version](https://img.shields.io/badge/unity-2022.3+-000.svg)](https://unity3d.com/get-unity/download/archive)
![Tests](https://img.shields.io/badge/Tests-Passed-brightgreen.svg)

## Table of Contents
- [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Manual Installation](#manual-installation)
    - [UPM Installation](#upm-installation)
- [Features](#features)
- [Usage](#usage)
  - [Define your systems](#define-your-systems)
  - [Declare dependencies via attributes](#declare-dependencies-via-attributes)
  - [Build an initialization context](#build-an-initialization-context)
  - [Tuning dependencies manually](#tuning-dependencies-manually)
  - [Listening to per-system callbacks](#listening-to-per-system-callbacks)
- [API Reference](#api-reference)
  - [IInitializable](#iinitializable)
  - [InitDependencyAttribute](#initdependencyattribute)
  - [InitializationContextBuilder](#initializationcontextbuilder)
  - [InitializationContext](#initializationcontext)
  - [IInitializationNodeHandle](#initializationnodehandle)
- [Dependencies](#dependencies)
- [License](#license)

## Getting Started

### Prerequisites
- [GIT](https://git-scm.com/downloads)
- [Unity](https://unity.com/releases/editor/archive) 2022.3+

### Manual Installation
1. Download the .unitypackage from the [releases](https://github.com/DanilChizhikov/pulse/releases/) page.
2. Import com.dtech.pulse.x.x.x.unitypackage into your project.

### UPM Installation
1. Open the manifest.json file in your project's Packages folder.
2. Add the following line to the dependencies section:
    ```json
    "com.dtech.pulse": "https://github.com/DanilChizhikov/pulse.git",
    ```
3. Unity will automatically import the package.

If you want to set a target version, Logging uses the `v*.*.*` release tag so you can specify a version like #v0.1.0.

For example `https://github.com/DanilChizhikov/pulse.git#v0.1.0`.

## Features
- **Attribute–based dependency discovery**
  
  System automatically discovers dependencies between systems using the `InitDependencyAttribute` on:
  - fields,
  - properties,
  - methods parameters,
  - constructors.
    It then builds a dependency graph and orders initialization accordingly.


- **Deterministic, batched initialization**

  Systems are initialized in topological order, grouped into batches.
  All systems in the same batch are initialized in parallel (as `Task`s), and the next batch starts only after the current one is done.


- **Strong validation of dependencies**
  During `Build()` Initialization:
  - verifies that every declared dependency has a corresponding system registered via `AddSystem`;
  - detects **cyclic dependencies** and fails fast with a clear error.

  This prevents situations where initialization silently “hangs” due to unresolved or circular dependencies.


- **Manual fine‑tuning of dependencies**

  For each added system you can:
  - add dependencies manually (`AddDependency`, `AddDependencies`);
  - remove or override automatically discovered dependencies (`RemoveDependency`, `RemoveDependencies`).

  This lets you adjust the dependency graph without changing the system’s implementation.


- **Critical systems support**
  
  You can mark systems as `critical`.

  The `InitializationContext` tracks them and raises `OnCriticalSystemsInitialized` once all critical systems are successfully initialized.


- **Initialization callbacks per system**

  For each system you can subscribe to:
  - `OnStartInitialize` — called when initialization of a specific system starts;
  - `OnCompleteInitialize` — called when initialization of a specific system finishes.

  This is useful for logging, profiling, or progress reporting.


- **Cancellation support**

  `InitializationContext.InitializationAsync` takes a `CancellationToken`.

  If the token is canceled:
  - Pulse stops processing further batches;
  - any already running tasks can respect the token and exit early.

## Usage

### Define your systems
Each system must implement IInitializable:
```csharp
using System.Threading;
using System.Threading.Tasks;
using DTech.Pulse;

public sealed class DatabaseSystem : IInitializable
{
    public Task InitializeAsync(CancellationToken token)
    {
        // Connect to DB, run migrations, etc.
        return Task.CompletedTask;
    }
}

public sealed class AuthSystem : IInitializable
{
    private readonly DatabaseSystem _database;

    public AuthSystem(DatabaseSystem database)
    {
        _database = database;
    }

    public Task InitializeAsync(CancellationToken token)
    {
        // Initialize auth flow, cache, etc.
        return Task.CompletedTask;
    }
}
```

### Declare dependencies via attributes
You can mark dependencies so Pulse can discover them automatically:
```csharp
using DTech.Pulse;

public sealed class GameplaySystem : IInitializable
{
    [InitDependency] 
    private AuthSystem _authSystem;

    public Task InitializeAsync(CancellationToken token)
    {
        // Game logic that requires auth.
        return Task.CompletedTask;
    }
}
```

You can also place `InitDependency` on:
- properties:
  ```csharp
  [InitDependency]
  private DatabaseSystem Database { get; set; }
  ```
- methods (on parameters):
  ```csharp
  [InitDependency]
  private void Setup(AuthSystem auth, GameplaySystem gameplay) { }
  ```
- constructors (or select a constructor with `InitDependency` if there are multiple):
  ```csharp
  public sealed class AnalyticsSystem : IInitializable
  {
      private readonly DatabaseSystem _database;
  
      [InitDependency]
      pulic AnalyticsSystem(DatabaseSystem database)
      {
          _database = database;
      }
      
      pulic AnalyticsSystem(DatabaseSystem database, AuthSystem auth)
      {
          _database = database;
      }
  
      public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
  }
  ```
  All collected dependency types are filtered to only those implementing IInitializable.

### Build an initialization context
In your bootstrap code (e.g. in a Unity entry point):
```csharp
using System.Threading;
using DTech.Pulse;

public sealed class GameBootstrap
{
    private InitializationContext _context;

    public void Setup()
    {
        var builder = new InitializationContextBuilder();

        var db = new DatabaseSystem();
        var auth = new AuthSystem(db);
        var gameplay = new GameplaySystem();
        var analytics = new AnalyticsSystem(db);

        builder.AddSystem(db).SetCritical();            // DB is critical
        builder.AddSystem(auth);                        // depends on DB via ctor
        builder.AddSystem(gameplay);                    // depends on Auth via [InitDependency]
        builder.AddSystem(analytics);                   // depends on DB via [InitDependency] ctor

        _context = builder.Build();
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        // Optional: react when all critical systems are ready
        _context.OnCriticalSystemsInitialized += () =>
        {
            // e.g. show main menu
        };

        await _context.InitializationAsync(token);
    }
}
```

### Tuning dependencies manually
You can add or remove dependencies programmatically, on top of what attributes determined
```csharp
var builder = new InitializationContextBuilder();

var db = new DatabaseSystem();
var auth = new AuthSystem(db);

// Add systems
IInitializationNodeHandle dbNode   = builder.AddSystem(db);
IInitializationNodeHandle authNode = builder.AddSystem(auth);

// Add an extra dependency explicitly
authNode.AddDependency<DatabaseSystem>();

// Remove all dependencies that are assignable to some base type:
authNode.RemoveDependencies(typeof(IInitializable)); // example: aggressively remove
```

If you accidentally reference a dependency that was **not** added to the builder, `Build()` will throw an exception:
```csharp
var builder = new InitializationContextBuilder();
builder.AddSystem(auth).AddDependency<DatabaseSystem>(); // DatabaseSystem not added

// Throws:
var context = builder.Build();
```

### Listening to per-system callbacks
You can attach callbacks to know when a specific system starts or completes initialization:
```csharp
builder.AddSystem(db)
       .OnStartInitialize(type => Debug.Log($"Start init: {type.Name}"))
       .OnCompleteInitialize(type => Debug.Log($"Complete init: {type.Name}"));
```

## API Reference
This section covers the main public types. Internal types are not part of the public API and may change.

### IInitializable
  ```csharp
  public interface IInitializable
  {
    Task InitializeAsync(CancellationToken token);
  }
  ```
  Interface that all systems must implement.

#### Task InitializeAsync(CancellationToken token)
  
  Called by Pulse during the initialization pass.
  - Use `token` to support cancellation (e.g. abort long-running operations).
  - May perform async work (network calls, I/O, loading, etc).

### InitDependencyAttribute
  ```csharp
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Constructor)]
  public sealed class InitDependencyAttribute : Attribute
  {
  }
  ```
Marks a member that declares dependencies of the system:
- **Fields / Properties**: the member type is treated as a dependency.
- **Methods**: all parameter types are treated as dependencies.
- **Constructors**: all parameter types are treated as dependencies.
If multiple constructors exist:
  - The one with InitDependencyAttribute is preferred;
  - Otherwise, the first public constructor is used.

Only types that implement IInitializable are kept as dependencies.

**Example:**
```csharp
public sealed class InventorySystem : IInitializable
{
    [InitDependency]
    private GameplaySystem _gameplay;

    [InitDependency]
    private void Setup(AuthSystem auth, DatabaseSystem db) { }

    public Task InitializeAsync(CancellationToken token) => Task.CompletedTask;
}
```

### InitializationContextBuilder
```csharp
public sealed class InitializationContextBuilder
{
    public IInitializationNodeHandle AddSystem(IInitializable system);
    public InitializationContext Build();
}
```

Responsible for registering systems and building the initialization plan.

#### `AddSystem(IInitializable system)`
Registers a system for initialization and returns an `IInitializationNodeHandle` to configure it.
- Automatically discovers dependencies using `InitDependencyAttribute` and constructors.
- Throws if a system with the same runtime type has already been added (depending on your current version; if not present you can easily add this check).

**Example:**
```csharp
var builder = new InitializationContextBuilder();

var db = new DatabaseSystem();
var auth = new AuthSystem(db);

builder.AddSystem(db)
       .SetCritical();

builder.AddSystem(auth)
       .OnStartInitialize(type => Console.WriteLine($"Init {type.Name}..."));
```

#### Build()
Builds the dependency graph, creates batches, and returns an `InitializationContext`.
Validation performed during `Build()`:
- **Missing dependencies**: if any system depends on a type that has no matching system registered, Build() throws an exception.
- **Cyclic dependencies**: if a cycle is detected, `Build()` throws an exception with the list of involved systems.

### InitializationContext
```csharp
public sealed class InitializationContext
{
    public event Action OnCriticalSystemsInitialized;

    public Task InitializationAsync(CancellationToken token);
}
```
Represents a compiled initialization plan.

#### `OnCriticalSystemsInitialized`
Event fired once all critical systems (marked via SetCritical()) have completed initialization.

**Example:**
```csharp
context.OnCriticalSystemsInitialized += () =>
{
    Debug.Log("All critical systems are ready!");
};
```

#### InitializationAsync(CancellationToken token)
Runs initialization:
- Systems are executed in batches according to their dependencies.
- Systems within the same batch are initialized in parallel using `Task.WhenAll`.
- If `token` is canceled:
  - The method returns early after the current batch is done.
  - Remaining batches are not processed.

**Example:**
```csharp
var cts = new CancellationTokenSource();

try
{
    await context.InitializationAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Handle cancellation if your systems throw it.
}
```

### IInitializationNodeHandle
```csharp
public interface IInitializationNodeHandle
{
    Type SystemType { get; }

    IInitializationNodeHandle AddDependency<T>() where T : IInitializable;
    IInitializationNodeHandle AddDependencies(params Type[] dependencies);

    IInitializationNodeHandle RemoveDependency<T>() where T : IInitializable;
    IInitializationNodeHandle RemoveDependencies(params Type[] dependencies);

    IInitializationNodeHandle SetCritical();

    IInitializationNodeHandle OnStartInitialize(Action<Type> callback);
    IInitializationNodeHandle OnCompleteInitialize(Action<Type> callback);
}
```
Fluent configuration interface returned by `AddSystem`.

#### SystemType
The runtime type of the registered system.

#### AddDependency<T>() / AddDependencies(params Type[] types)
Adds explicit dependencies.

All types must implement `IInitializable`, otherwise an exception is thrown.
**Example:**
```csharp
builder.AddSystem(gameplay)
       .AddDependency<AuthSystem>();
```

#### RemoveDependency<T>() / RemoveDependencies(params Type[] types)
Removes dependencies that were previously added or discovered.
- Removal uses `IsAssignableFrom`, so passing a base type removes all compatible dependencies.

**Example:**
```csharp
// Remove a specific dependency
node.RemoveDependency<AuthSystem>();

// Remove all dependencies implementing some base interface
node.RemoveDependencies(typeof(IMySubsystemBase));
```

#### SetCritical()
Marks this system as **critical**.
The `InitializationContext` will track it and only raise `OnCriticalSystemsInitialized` once all critical systems are done.
```csharp
builder.AddSystem(db).SetCritical();
```

#### OnStartInitialize(Action<Type> callback) / OnCompleteInitialize(Action<Type> callback)
Registers callbacks for a particular system:
```csharp
builder.AddSystem(db)
       .OnStartInitialize(type => Debug.Log($"Start: {type.Name}"))
       .OnCompleteInitialize(type => Debug.Log($"Done: {type.Name}"));
```

If you want, I can also add a small “Quick Start” snippet above these sections showing a full minimal example from builder setup to initialization call.

## Dependencies
- [Performance Testing Package for Unity v3.2.0](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/index.html)

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.