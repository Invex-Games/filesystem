# Introduction

`Invex.FileSystem` is a strongly-typed file system library for .NET. It builds on
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

```
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

## Next steps

Head to [Getting Started](getting-started.md) to install the package and wire it into your dependency-injection
container.

