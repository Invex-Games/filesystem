# Invex.FileSystem

[![NuGet](https://img.shields.io/nuget/v/Invex.FileSystem.svg)](https://www.nuget.org/packages/Invex.FileSystem)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4.svg)](https://dotnet.microsoft.com/)

A strongly-typed file system abstraction for .NET that layers **build-aware path resolution** and **reversible file
transformations** on top of [`System.IO.Abstractions`](https://github.com/TestableIO/System.IO.Abstractions).

Stop hard-coding paths and scattering magic strings across your build and tooling code. `Invex.FileSystem` lets you
resolve well-known locations by key, compose paths fluently, and make temporary, automatically-rolled-back edits to
files.

---

## Features

### Rooted paths

`RootedPath` carries its own `IFileSystem` so paths and I/O always travel together. Compose them
fluently with the `/` operator.

### Keyed path resolution

resolve locations such as `"Root"` or `"Artifacts"` through a prioritized chain of
`IPathProvider` implementations, with results cached after first lookup.

### Strongly-typed path markers

give compile-time names to well-known paths via `IPathMarker` instead of relying on
magic strings.

### Reversible file transforms

`TransformFileScope` and `TransformMultiFileScope` apply temporary edits to one or
many files and restore the originals on dispose (or commit them with `CancelRestore`).

### Dependency-injection first

a single `AddRootedFileSystem()` call wires everything up; register custom providers
with `ProvidePath(...)`.

## Installation

```shell
dotnet add package Invex.FileSystem
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

## Core concepts

### `RootedPath`

A `RootedPath` is a `record` that binds an absolute path string to the `IFileSystem` it belongs to. Because the file
system travels with the path, existence checks and queries need no extra context:

```csharp
RootedPath path = fs.GetPath("Root") / "src" / "Project.csproj";

bool exists          = path.PathExists;             // file or directory
bool isFile          = path.FileExists;
RootedPath? parent   = path.Parent;                 // null at the root
string nameNoExt     = path.FileNameWithoutExtension;

// Implicitly converts to string for APIs that expect one.
File.ReadAllText(path);
```

Use the `/` operator to combine segments and the implicit `string` conversion to pass a path to any standard API.

### Path providers

Path resolution is decoupled from I/O. `IRootedFileSystem.GetPath(string)` queries every registered `IPathProvider` in *
*descending priority order** and returns the first non-`null` result, caching it for subsequent calls.

Register providers inline with `ProvidePath`:

```csharp
// Simple key -> path mapping (priority defaults to 1).
services.ProvidePath(key => key == "Temp"
    ? new RootedPath(new FileSystem(), Path.GetTempPath())
    : null);

// Provider that depends on another resolved path.
services.ProvidePath((key, fs) => key == "Output"
    ? fs.GetPath("Root") / "output"
    : null, priority: 5);
```

For more complex resolution logic, implement `IPathProvider` directly and register it as a service. Higher `Priority`
values win when multiple providers can resolve the same key; use a negative priority for a low-priority fallback.

### Path markers

When a path is statically determined by its type, implement `IPathMarker` to give it a strongly-typed name. Unlike keyed
lookups, markers bypass the provider chain and cache:

```csharp
public sealed class ArtifactsPath : IPathMarker
{
    public static RootedPath Path(IFileSystem fileSystem) =>
        ((IRootedFileSystem)fileSystem).GetPath("Root") / "artifacts";
}

RootedPath artifacts = fs.GetPath<ArtifactsPath>();
```

### Reversible file transforms

`TransformFileScope` captures a file's content, applies a transform, and restores the original when the scope is
disposed — ideal for build steps that must temporarily patch a file (such as injecting a version number) without leaving
permanent changes.

```csharp
RootedPath projectFile = fs.GetPath("Root") / "Directory.Build.props";

await using (var scope = await TransformFileScope.CreateAsync(
    projectFile,
    content => content.Replace("1.0.0", "2.0.0")))
{
    // The file now contains "2.0.0" — run your build step here.
}
// On dispose the original content is restored automatically.
```

Chain additional edits fluently, and call `CancelRestore()` to keep the changes instead of rolling them back:

```csharp
await using var scope = await TransformFileScope
    .CreateAsync(projectFile, _ => "version=1")
    .AddAsync(content => content + "\nbuild=ci");

scope.CancelRestore(); // commit the transformation permanently
```

If a file does not exist when the scope is created, it is created during the scope and **deleted** on restore.

Use `TransformMultiFileScope` to patch a set of files in parallel with the same all-or-nothing restore guarantee:

```csharp
RootedPath[] projects =
[
    fs.GetPath("Root") / "src" / "A" / "A.csproj",
    fs.GetPath("Root") / "src" / "B" / "B.csproj",
];

await using var scope = await TransformMultiFileScope.CreateAsync(
    projects,
    content => content.Replace("<Version>1.0.0</Version>", "<Version>2.0.0</Version>"));
```

## Testing

Because the library builds on `System.IO.Abstractions`, you can construct a `RootedPath` over a `MockFileSystem` and
exercise your code without touching the real disk:

```csharp
var mock = new MockFileSystem();
var path = new RootedPath(mock, "/repo/file.txt");

mock.AddFile(path, new MockFileData("hello"));
path.FileExists.ShouldBeTrue();
```

## API reference

| Type                       | Description                                                                         |
|----------------------------|-------------------------------------------------------------------------------------|
| `IRootedFileSystem`        | File system abstraction with `GetPath`, `CurrentDirectory`, and `CreateRootedPath`. |
| `RootedPath`               | A path bound to an `IFileSystem`, with composition and existence helpers.           |
| `IPathProvider`            | Resolves a `RootedPath` for a key; queried by priority.                             |
| `FunctionPathProvider`     | Delegate-based `IPathProvider` implementation.                                      |
| `IPathMarker`              | Strongly-typed, statically-computed path.                                           |
| `TransformFileScope`       | Temporary, reversible single-file transformation.                                   |
| `TransformMultiFileScope`  | Temporary, reversible multi-file transformation.                                    |
| `FileSystemHostExtensions` | DI registration: `AddRootedFileSystem` and `ProvidePath`.                           |

## License

Licensed under the [MIT License](LICENSE.txt).
