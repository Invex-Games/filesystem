# Invex.FileSystem

A strongly-typed file system abstraction for .NET that layers **build-aware path resolution** and **reversible file
transformations** on top of [`System.IO.Abstractions`](https://github.com/TestableIO/System.IO.Abstractions).
Stop hard-coding paths and scattering magic strings across your build and tooling code: resolve well-known locations
by key, compose paths fluently, and make temporary, automatically-rolled-back edits to files.

## Start here

- [Introduction](docs/introduction.md): what the library solves and when to use it.
- [Getting Started](docs/getting-started.md): install the package, register it and resolve your first paths.
- [Path Providers](docs/path-providers.md): resolve well-known locations by key.
- [File Transform Scopes](docs/transform-scopes.md): temporary, reversible file edits.
- [API Reference](api/index.md): every public type.
