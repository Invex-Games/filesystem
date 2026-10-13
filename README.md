# Invex.FileSystem

[![Build](https://github.com/Invex-Games/filesystem/actions/workflows/Build.yml/badge.svg)](https://github.com/Invex-Games/filesystem/actions/workflows/Build.yml)
[![NuGet](https://img.shields.io/nuget/v/Invex.FileSystem.svg)](https://www.nuget.org/packages/Invex.FileSystem)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Invex-Games/filesystem/blob/main/LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4.svg)](https://dotnet.microsoft.com/)

A strongly-typed file system abstraction for .NET that layers **build-aware path resolution** and **reversible file
transformations** on top of [`System.IO.Abstractions`](https://github.com/TestableIO/System.IO.Abstractions).
Stop hard-coding paths and scattering magic strings across your build and tooling code: resolve well-known locations
by key, compose paths fluently, and make temporary, automatically-rolled-back edits to files.

## Features

- **Rooted paths.** `RootedPath` carries its own `IFileSystem`, so paths and I/O always travel together, and composes
  fluently with the `/` operator.
- **Keyed path resolution.** Resolve locations such as `"Root"` or `"Artifacts"` through a prioritized chain of
  `IPathProvider` implementations, with results cached after the first lookup.
- **Strongly-typed path markers.** Give compile-time names to well-known paths via `IPathMarker` instead of relying on
  magic strings.
- **Reversible file transforms.** `TransformFileScope` and `TransformMultiFileScope` apply temporary edits to one or
  many files and restore the originals on dispose, or commit them with `CancelRestore()`.
- **Dependency-injection first.** A single `AddRootedFileSystem()` call wires everything up; register custom providers
  with `ProvidePath(...)`.
- **Testable.** Everything works against `IFileSystem`, so a `MockFileSystem` drops straight into your tests.

## Installation

```shell
dotnet add package Invex.FileSystem
```

The package targets **.NET 8.0**, **.NET 9.0** and **.NET 10.0**. It depends on `System.IO.Abstractions`,
`Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Hosting.Abstractions`. The rooted file
system logs path resolutions through `ILogger`, so register logging (for example `services.AddLogging()`) in your
container. Generic-host apps already have both; a standalone console or build tool also needs the
`Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Logging` packages for `BuildServiceProvider()` and
`AddLogging()`:

```shell
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging
```

## Quick start

Register the file system with your DI container and declare where your well-known paths live:

```csharp
using Invex.FileSystem;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();

// Register the rooted file system.
services.AddRootedFileSystem();

// Describe where your well-known paths live.
services.ProvidePath((key, fs) => key switch
{
    "Root"      => fs.CurrentDirectory,
    "Artifacts" => fs.GetPath("Root") / "artifacts",
    _           => null,
});

using var provider = services.BuildServiceProvider();
var fs = provider.GetRequiredService<IRootedFileSystem>();

// Resolve and compose paths.
RootedPath artifacts = fs.GetPath("Artifacts");
RootedPath logFile   = artifacts / "logs" / "build.log";

if (!logFile.PathExists)
    fs.Directory.CreateDirectory(artifacts);
```

## Usage

### Compose and query paths

A `RootedPath` binds an absolute path string to the `IFileSystem` it belongs to, so existence checks and queries need
no extra context. It implicitly converts to `string` for any API that expects one:

```csharp
RootedPath path = fs.GetPath("Root") / "src" / "Project.csproj";

bool exists        = path.PathExists;   // file or directory
RootedPath? parent = path.Parent;       // null at the root

File.ReadAllText(path);
```

See [Rooted Paths](https://github.com/Invex-Games/filesystem/blob/main/docs/rooted-paths.md).

### Resolve well-known locations by key

`IRootedFileSystem.GetPath(string)` queries every registered `IPathProvider` in descending priority order and caches
the first non-`null` result. Use the `(key, fs)` overload of `ProvidePath` when a path depends on another resolved path:

```csharp
services.ProvidePath((key, fs) => key == "Output"
    ? fs.GetPath("Root") / "output"
    : null, priority: 5);
```

See [Path Providers](https://github.com/Invex-Games/filesystem/blob/main/docs/path-providers.md).

### Name paths with types

Implement `IPathMarker` when a path is statically determined by a type. Markers bypass the provider chain and cache:

```csharp
public sealed class ArtifactsPath : IPathMarker
{
    public static RootedPath Path(IFileSystem fileSystem) =>
        ((IRootedFileSystem)fileSystem).GetPath("Root") / "artifacts";
}

RootedPath artifacts = fs.GetPath<ArtifactsPath>();
```

See [Path Markers](https://github.com/Invex-Games/filesystem/blob/main/docs/path-markers.md).

### Patch files temporarily

`TransformFileScope` captures a file's content, applies a transform and restores the original when the scope is
disposed. `TransformMultiFileScope` does the same for a set of files:

```csharp
await using (var scope = await TransformFileScope.CreateAsync(
    fs.GetPath("Root") / "Directory.Build.props",
    content => content.Replace("1.0.0", "2.0.0")))
{
    // The file now contains "2.0.0"; run your build step here.
}
// On dispose the original content is restored automatically.
```

See [File Transform Scopes](https://github.com/Invex-Games/filesystem/blob/main/docs/transform-scopes.md).

### Test without touching the disk

Construct a `RootedPath` over a `MockFileSystem`, or override the keyed `"RootedFileSystem"` registration in your
container:

```csharp
var mock = new MockFileSystem();
var path = new RootedPath(mock, "/repo/file.txt");

mock.AddFile(path, new MockFileData("hello"));
// path.FileExists is now true
```

See [Testing](https://github.com/Invex-Games/filesystem/blob/main/docs/testing.md).

## Documentation

Full documentation: [https://invex-games.github.io/filesystem/](https://invex-games.github.io/filesystem/)

| Guide                                                                                                | Contents                                                              |
|------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------|
| [Introduction](https://github.com/Invex-Games/filesystem/blob/main/docs/introduction.md)             | What the library solves, when to use it, and the pieces at a glance.  |
| [Getting Started](https://github.com/Invex-Games/filesystem/blob/main/docs/getting-started.md)       | Installation, DI registration, and your first resolved paths.         |
| [Rooted Paths](https://github.com/Invex-Games/filesystem/blob/main/docs/rooted-paths.md)             | Creating, composing and querying `RootedPath`.                        |
| [Path Providers](https://github.com/Invex-Games/filesystem/blob/main/docs/path-providers.md)         | Priorities, caching, custom providers and error handling.             |
| [Path Markers](https://github.com/Invex-Games/filesystem/blob/main/docs/path-markers.md)             | Strongly-typed paths with `IPathMarker`.                              |
| [File Transform Scopes](https://github.com/Invex-Games/filesystem/blob/main/docs/transform-scopes.md) | Temporary, reversible single- and multi-file edits.                  |
| [Testing](https://github.com/Invex-Games/filesystem/blob/main/docs/testing.md)                       | Using `MockFileSystem` with rooted paths and transform scopes.        |
| [API Reference](https://github.com/Invex-Games/filesystem/blob/main/api/index.md)                    | Every public type, with links into the generated reference.           |

## Development

```shell
dotnet build Invex.FileSystem.slnx
dotnet test Invex.FileSystem.slnx
```

The documentation site is built with [DocFX](https://dotnet.github.io/docfx/) through the
[Atom](https://github.com/Invex-Games/atom) build in `_atom/`:

```shell
atom BuildDocs   # generate the site
atom ServeDocs   # build and serve it locally
```

Without the `atom` tool installed, run the same targets with `dotnet run --project _atom -- BuildDocs`.

## License

Licensed under the [MIT License](https://github.com/Invex-Games/filesystem/blob/main/LICENSE.txt).
