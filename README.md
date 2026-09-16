# Pulse
[![Unity Version](https://img.shields.io/badge/unity-6000.0+-000.svg)](https://unity3d.com/get-unity/download/archive)
![Unity Tests](https://github.com/DanilChizhikov/pulse/actions/workflows/tests.yml/badge.svg?branch=master)

## Table of Contents
- [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Manual Installation](#manual-installation)
    - [UPM Installation](#upm-installation)
- [Features](#features)
- [Runtime compatibility](#runtime-compatibility)
- [Usage](#usage)
  - [Define your systems](#define-your-systems)
  - [Declare dependencies via attributes](#declare-dependencies-via-attributes)
  - [Build an initialization context](#build-an-initialization-context)
  - [Tuning dependencies manually](#tuning-dependencies-manually)
  - [Listening to per-system callbacks](#listening-to-per-system-callbacks)
  - [Recording the initialization graph](#recording-the-initialization-graph)
- [API Reference](#api-reference)
  - [IInitializable](#iinitializable)
  - [InitDependencyAttribute](#initdependencyattribute)
  - [InitializationContextBuilder](#initializationcontextbuilder)
  - [InitializationContext](#initializationcontext)
  - [IInitializationNodeHandle](#initializationnodehandle)
  - [IInitializationFramePacer](#iinitializationframepacer)
  - [InitializationGraphRecording](#initializationgraphrecording)
- [Dependencies](#dependencies)
- [License](#license)

## Getting Started

### Prerequisites
- [GIT](https://git-scm.com/downloads)
- [Unity](https://unity.com/releases/editor/archive) 6000.0+

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

If you want to set a target version, Pulse uses the `v*.*.*` release tag so you can specify a version like #v2.0.0.

For example `https://github.com/DanilChizhikov/pulse.git#v2.0.0`.

## Features
- **Attribute–based dependency discovery**
  
  System automatically discovers dependencies between systems using the `InitDependencyAttribute` on:
  - fields,
  - properties,
  - methods parameters,
  - constructors.
    It then builds a dependency graph and orders initialization accordingly.


- **Dependency-driven scheduling**

  Every system starts as soon as its own dependencies are initialized — nothing waits for unrelated systems.
  Independent systems run in parallel (as `Task`s), so the duration of a run is the length of the critical path
  instead of the sum of the slowest system of every dependency level.


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
  - Pulse stops starting new systems;
  - already running systems are awaited, and they can respect the token and exit early.


- **Optional frame pacing**

  Pass an `IInitializationFramePacer` to postpone the next system while the current frame is already too long.
  `PlayerLoopFramePacer` is the built-in implementation: it hooks into the player loop (no `MonoBehaviour` involved)
  and yields a frame whenever `Time.unscaledDeltaTime` exceeds the configured budget. Disabled unless a pacer is set.


- **Initialization graph recording (opt-in)**

  Record dependencies, start order and timings of every system and inspect them in a GraphView window
  (`Window/DTech/Pulse/Initialization Graph`). Disabled by default, so it doesn't affect regular runs.

## Runtime compatibility

Pulse intentionally uses `System.Threading.Tasks.Task` in its public API. This keeps the package free from an async
runtime dependency and lets consumers adapt Pulse from UniTask, coroutines, or plain .NET async code.

Pulse preserves its own runtime assembly metadata for IL2CPP builds. If your systems declare dependencies through
private fields, private properties, private methods, or constructors, Unity managed stripping can still remove metadata
from your consumer code. For high stripping levels, add `[UnityEngine.Scripting.Preserve]` to those consumer types or
members, or register dependencies manually with `AddDependency` / `AddDependencies`.

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
      public AnalyticsSystem(DatabaseSystem database)
      {
          _database = database;
      }
      
      public AnalyticsSystem(DatabaseSystem database, AuthSystem auth)
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

        builder.AddSystem(db).SetAsCritical();            // DB as critical
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

### Recording the initialization graph
Pulse can record how an initialization actually went: dependencies, start order, start offset, duration
and status of every system. Recording is **disabled by default** — when it is off, no recorder is created and regular
runs are not affected.

**In the Editor**
1. Enable `Tools/DTech/Pulse/Record Initialization Graph` (stored in `EditorPrefs`).
2. Enter Play Mode. Every time an `InitializationContext` finishes (completed, cancelled or failed), a snapshot is saved
   to `Library/Pulse/Graphs` (the last 20 snapshots are kept).
3. Open `Window/DTech/Pulse/Initialization Graph` to browse snapshots:
   - systems are grouped into columns by their dependency level, and the group title shows the span of the level;
   - edges go from a dependency to the systems that depend on it. Edges already implied by another dependency
     (`A → B → C` makes `A → C` redundant) are hidden; enable **All Edges** in the toolbar to draw them too;
   - select one or more systems to see all of their direct edges; systems they are not linked to are dimmed;
   - edges mirror the recorded dependencies and cannot be selected, deleted or reconnected;
   - each node shows start order, level, start offset, duration and status; times below 500 ms are shown
     in milliseconds, longer ones in seconds (`0.51 s`);
   - the node header goes from green (fast) to red (the slowest system); critical systems have a `CRITICAL` badge.

**From code (e.g. in a player build)**
```csharp
InitializationGraphRecording.IsEnabled = true; // must be set before builder.Build()

string path = Path.Combine(Application.persistentDataPath, "pulse-graph.xml");

//Can be called from non-main thread
InitializationGraphRecording.OnSnapshotRecorded += snapshot =>
{
    File.WriteAllText(path, snapshot.ToXml(true));
};
```
Copy the XML to your machine and load it with **Open File...** in the Initialization Graph window.

**Getting the file off the device**

Android (`persistentDataPath` is `/storage/emulated/0/Android/data/<package-name>/files`):
```bash
adb shell run-as <package-name> ls files                       # sanity check for non-debuggable paths
adb pull /storage/emulated/0/Android/data/<package-name>/files/pulse-graph.xml .
```
For a non-debuggable release build the app-private path is not readable over `adb pull`; either use a debuggable
build, or write the snapshot somewhere you can read (`adb shell run-as <package-name> cat files/pulse-graph.xml > pulse-graph.xml`).

iOS (`persistentDataPath` is `<app container>/Documents`):
1. Xcode -> `Window/Devices and Simulators` -> select the device -> **Installed Apps** -> select the app.
2. `...` (gear) -> **Download Container...** and save the `.xcappdata` bundle.
3. Right-click the bundle -> **Show Package Contents** -> `AppData/Documents/pulse-graph.xml`.

The app has to be installed with a development profile for **Download Container** to be available. If you want the
file to show up in the Files app instead, enable `UIFileSharingEnabled` / `LSSupportsOpeningDocumentsInPlace` in
`Info.plist` and copy it out over USB.

Notes:
- `IsEnabled` is read once per `Build()` call.
- `OnSnapshotRecorded` may be raised on a non-main thread if your systems continue on the thread pool.
- The measured duration of a system includes its callbacks (`OnStartInitialize` / `OnCompleteInitialize` and context events).

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
  - Otherwise, Build fails and asks you to mark one constructor explicitly.

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
    public InitializationContextBuilder SetFramePacer(IInitializationFramePacer framePacer);
    public InitializationContext Build();
}
```

Responsible for registering systems and building the initialization plan.

#### `AddSystem(IInitializable system)`
Registers a system for initialization and returns an `IInitializationNodeHandle` to configure it.
- Automatically discovers dependencies using `InitDependencyAttribute` and constructors.
- Throws if a system with the same runtime type has already been added.

**Example:**
```csharp
var builder = new InitializationContextBuilder();

var db = new DatabaseSystem();
var auth = new AuthSystem(db);

builder.AddSystem(db)
       .SetAsCritical();

builder.AddSystem(auth)
       .OnStartInitialize(type => Console.WriteLine($"Init {type.Name}..."));
```

#### `SetFramePacer(IInitializationFramePacer framePacer)`
Sets the pacer used to postpone systems while the current frame is overloaded. Pass `null` (the default) to
initialize without any frame gate.

**Example:**
```csharp
var builder = new InitializationContextBuilder();
builder.SetFramePacer(new PlayerLoopFramePacer(maxFrameSeconds: 0.1f));
```

#### Build()
Builds and validates the dependency graph and returns an `InitializationContext`.
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

#### OnSystemInitializationBegan
Event fired when a system begins initialization system.

**Example:**
```csharp
context.OnSystemInitializationBegan += type =>
{
    Debug.Log($"Init {type.Name}...");
};
```

#### OnSystemInitializationComplete
Event fired when a system complete initialization.

**Example:**
```csharp
context.OnSystemInitializationComplete += type =>
{
    Debug.Log($"Init {type.Name}...");
};
```

#### OnCriticalSystemsInitialized
Event fired once all critical systems (marked via SetAsCritical()) have completed initialization.

**Example:**
```csharp
context.OnCriticalSystemsInitialized += () =>
{
    Debug.Log("All critical systems are ready!");
};
```

#### InitializationAsync(CancellationToken token)
Runs initialization:
- Every system starts as soon as its own dependencies are initialized; independent systems run in parallel.
- If a system throws, no new system is started, the already running ones are awaited and the exception is rethrown.
- If `token` is canceled:
  - No new system is started.
  - The already running systems are awaited and the method returns normally.

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

    IInitializationNodeHandle SetAsCritical();

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

#### SetAsCritical()
Marks this system as **critical**.
The `InitializationContext` will track it and only raise `OnCriticalSystemsInitialized` once all critical systems are done.
```csharp
builder.AddSystem(db).SetAsCritical();
```

#### OnStartInitialize(Action<Type> callback) / OnCompleteInitialize(Action<Type> callback)
Registers callbacks for a particular system:
```csharp
builder.AddSystem(db)
       .OnStartInitialize(type => Debug.Log($"Start: {type.Name}"))
       .OnCompleteInitialize(type => Debug.Log($"Done: {type.Name}"));
```

### IInitializationFramePacer
```csharp
public interface IInitializationFramePacer
{
    bool IsFrameOverloaded { get; }
    Task WaitNextFrameAsync(CancellationToken token);
}
```
Optional throttling hook. When a pacer is set through `InitializationContextBuilder.SetFramePacer`, Pulse asks it
before the first system starts and after every system that has dependents: if `IsFrameOverloaded` is `true`,
the next systems are postponed until `WaitNextFrameAsync` completes.

Implementations must complete the returned task instead of throwing when the token is cancelled — the cancellation
itself is handled by `InitializationContext`.

`PlayerLoopFramePacer` is the built-in implementation:
```csharp
// 100 ms budget by default; create it on the main thread.
var framePacer = new PlayerLoopFramePacer(maxFrameSeconds: 0.1f);
builder.SetFramePacer(framePacer);
// ...
framePacer.Dispose(); // restores the original player loop; also happens on Application.quitting
```
It inserts its own `PlayerLoopSystem` into the `Update` phase on the first wait, so no `MonoBehaviour` is needed.
Outside Play Mode (`Application.isPlaying == false`) it never waits, and `IsFrameOverloaded` reports `false`
when queried from a thread other than the one it was created on.

### InitializationGraphRecording
```csharp
public static class InitializationGraphRecording
{
    public static event Action<InitializationGraphSnapshot> OnSnapshotRecorded;

    public static bool IsEnabled { get; set; }
}
```
Global switch for [initialization graph recording](#recording-the-initialization-graph).

#### IsEnabled
When `true`, contexts built afterwards record their initialization graph. Disabled by default.
In the Editor it is synced with the `Tools/DTech/Pulse/Record Initialization Graph` menu toggle.

#### OnSnapshotRecorded
Raised once per recorded `InitializationAsync` run with an `InitializationGraphSnapshot`:
- `Status` — `Completed`, `Cancelled` or `Failed`;
- `RecordedAtUtc`, `TotalMilliseconds`;
- `Systems` — per system: `TypeName`, `FullTypeName`, `StartOrder` (`-1` if not started), `IsCritical`,
  `Status`, `StartMilliseconds`, `DurationMilliseconds`, `DependencyIndices` (indices into `Systems`), `Error`.

Use `snapshot.ToXml()` / `InitializationGraphSnapshot.FromXml(xml)` to persist and restore snapshots.

## Dependencies
- [Performance Testing Package for Unity v3.2.0](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/index.html)

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
