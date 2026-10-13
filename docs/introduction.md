# Introduction

`Invex.FileSystem` is a strongly-typed file system library for .NET. This page explains the problems it solves, when
it is (and is not) a good fit, and the ideas the rest of the documentation builds on. It builds on
[`System.IO.Abstractions`](https://github.com/TestableIO/System.IO.Abstractions) and adds two core capabilities:

1. **Keyed path resolution** — resolve well-known locations (such as `"Root"` or `"Artifacts"`) by key through a
   prioritized, cached chain of providers, instead of hard-coding paths or scattering magic strings.
2. **Reversible file transformations** — apply temporary edits to one or many files inside a disposable scope, with
   the original content restored automatically when the scope is disposed.

## Why?

Build scripts, code generators, and developer tooling all share the same pain points:

- **Paths are stringly-typed.** A typo in `"artifcats/logs"` compiles fine and fails at runtime.
- **Paths are computed everywhere.** Each call site re-derives "the repo root" or "the output folder" in its own way,
  and they drift apart over time.
- **Temporary file edits leak.** A step that patches a version number into a project file and then crashes leaves the
  repository dirty.
- **File I/O is hard to test.** Code that calls `System.IO.File` directly needs a real disk to test against.

`Invex.FileSystem` addresses each of these:

| Pain point             | Solution                                                                                          |
|------------------------|---------------------------------------------------------------------------------------------------|
| Stringly-typed paths   | [`RootedPath`](rooted-paths.md) composes with the `/` operator and carries its own `IFileSystem`. |
| Duplicated path logic  | [`IPathProvider`](path-providers.md) resolves each key once, in one place, with caching.          |
| Leaky temporary edits  | [`TransformFileScope`](transform-scopes.md) restores original content on dispose, guaranteed.     |
| Untestable I/O         | Everything works against `IFileSystem`, so a `MockFileSystem` drops straight in ([Testing](testing.md)). |

## The pieces at a glance

```text
┌──────────────────────────────────────────────────────────┐
│ IRootedFileSystem  (implements IFileSystem)              │
│                                                          │
│   GetPath("Root")        ──► IPathProvider chain         │
│   GetPath<TMarker>()     ──► static marker resolution    │
│   CurrentDirectory       ──► RootedPath                  │
│   CreateRootedPath(...)  ──► RootedPath                  │
│                                                          │
│   File / Directory / Path / ...  ──► inner IFileSystem   │
└──────────────────────────────────────────────────────────┘
```

- **`IRootedFileSystem`** is a drop-in `IFileSystem` with path resolution layered on top. Code that only needs file
  I/O can keep depending on `IFileSystem`; code that needs well-known locations depends on `IRootedFileSystem`.
- **`RootedPath`** is a record pairing an absolute path string with the `IFileSystem` it belongs to, so existence
  checks and composition need no extra context.
- **`IPathProvider`** implementations answer "where is *X*?" — one provider per concern, queried in priority order.
- **`TransformFileScope`** / **`TransformMultiFileScope`** make temporary file edits safe.

## When to use it

`Invex.FileSystem` fits well when:

- You write **build scripts, code generators or developer tooling** that refer to the same handful of locations
  (repository root, artifacts, output folders) from many places.
- You need to **patch files for the duration of an operation**, such as stamping a version into a project file, and
  must leave the repository clean afterwards even when the operation fails.
- Your code already uses, or can move to, **`IFileSystem`** and dependency injection.

It is probably not the right tool when:

- You only need a mockable file system. Use `System.IO.Abstractions` directly; this library adds path resolution on
  top of it, not a different I/O model.
- You need a sandbox. A `RootedPath` is *bound* to a file system instance, not *confined* to a root directory, so it
  does not stop code from composing paths outside a folder.
- You need durable, crash-safe edits. Transform scopes keep the original content in memory and restore it on dispose;
  if the process is killed before disposal, nothing restores the file.

## Design principles

- **Paths travel with their file system.** Every `RootedPath` carries the `IFileSystem` it belongs to, so the same code
  runs against the real disk and a `MockFileSystem`.
- **Resolution is separate from I/O.** `IRootedFileSystem` is a drop-in `IFileSystem`; where a location lives is decided
  by providers registered once, not by the code that reads or writes files.
- **Resolve once, in one place.** Keyed lookups are answered by the highest-priority provider that recognizes the key
  and cached for the lifetime of the file system.
- **Prefer `null` to exceptions for "not that kind of entry".** Members such as `FileName` and `DirectoryName` return
  `null` when the path is not the expected entry type, so null-coalescing works naturally.
- **Restores always complete.** Disposing a transform scope accepts no cancellation token, so a file is never left
  half-restored.

## Next steps

- [Getting Started](getting-started.md): install the package and wire it into your dependency-injection container.
- [Rooted Paths](rooted-paths.md): everything `RootedPath` can do.
- [API Reference](../api/index.md): every public type.
