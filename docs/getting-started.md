# Getting Started

## Installation

```shell
dotnet add package Invex.FileSystem
```

The package targets **.NET 8.0**, **.NET 9.0**, and **.NET 10.0**.

## Registering with dependency injection

`Invex.FileSystem` is designed to be wired up through `Microsoft.Extensions.DependencyInjection`. A single call
registers everything:

```csharp
using Invex.FileSystem;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();          // IRootedFileSystem logs path resolutions at Debug level
services.AddRootedFileSystem();
```

`AddRootedFileSystem()` registers:

| Service                          | Lifetime  | Notes                                                                  |
|----------------------------------|-----------|------------------------------------------------------------------------|
| `IFileSystem` (keyed: `"RootedFileSystem"`) | Singleton | The real `System.IO.Abstractions.FileSystem` backing implementation. |
| `IRootedFileSystem`              | Singleton | The rooted wrapper, with all registered `IPathProvider`s sorted by descending priority. |
| `IFileSystem` (non-keyed)        | Singleton | Resolves to the same instance as `IRootedFileSystem`, so existing code that depends on `IFileSystem` automatically gets the rooted implementation. |

## Declaring well-known paths

After `AddRootedFileSystem()`, declare where your well-known locations live with `ProvidePath`:

```csharp
services.ProvidePath((key, fs) => key switch
{
    "Root"      => fs.CurrentDirectory,
    "Artifacts" => fs.GetPath("Root") / "artifacts",
    "Logs"      => fs.GetPath("Artifacts") / "logs",
    _           => null,    // defer to the next provider
});
```

Returning `null` for unrecognized keys is important — it lets other providers handle those keys. See
[Path Providers](path-providers.md) for priorities, ordering, and custom implementations.

## Resolving and using paths

```csharp
using var provider = services.BuildServiceProvider();
var fs = provider.GetRequiredService<IRootedFileSystem>();

// Resolve well-known locations by key.
RootedPath artifacts = fs.GetPath("Artifacts");

// Compose further with the / operator.
RootedPath logFile = artifacts / "logs" / "build.log";

// Query and perform I/O — RootedPath implicitly converts to string.
if (!logFile.PathExists)
    fs.Directory.CreateDirectory(logFile.Parent!);

fs.File.WriteAllText(logFile, "hello");
```

Key things to notice:

- `GetPath` results are **cached** — `fs.GetPath("Root")` always returns the same instance after the first call.
- `RootedPath` **implicitly converts to `string`**, so it can be passed to any API expecting a path.
- All I/O members (`File`, `Directory`, `Path`, …) come from `IFileSystem` and delegate to the inner file system.

## Without dependency injection

DI is convenient but not required. You can construct a `RootedPath` directly over any `IFileSystem`:

```csharp
using System.IO.Abstractions;

var fileSystem = new FileSystem();
var path = new RootedPath(fileSystem, @"C:\repos\my-project");

RootedPath csproj = path / "src" / "App" / "App.csproj";
bool exists = csproj.FileExists;
```

## Next steps

- [Rooted Paths](rooted-paths.md) — everything `RootedPath` can do.
- [Path Providers](path-providers.md) — priorities, chaining, and custom providers.
- [File Transform Scopes](transform-scopes.md) — temporary, reversible file edits.

