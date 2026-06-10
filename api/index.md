# API Reference

This section contains the generated API reference for every public type in the `Invex.FileSystem` namespace.

## Key types

| Type                                                          | Description                                                                          |
|---------------------------------------------------------------|--------------------------------------------------------------------------------------|
| @Invex.FileSystem.IRootedFileSystem                           | File system abstraction with `GetPath`, `CurrentDirectory`, and `CreateRootedPath`.  |
| @Invex.FileSystem.RootedPath                                  | A path bound to an `IFileSystem`, with composition and existence helpers.            |
| @Invex.FileSystem.IPathProvider                               | Resolves a `RootedPath` for a key; queried by priority.                              |
| @Invex.FileSystem.FunctionPathProvider                        | Delegate-based `IPathProvider` implementation.                                       |
| @Invex.FileSystem.IPathMarker                                 | Strongly-typed, statically-computed path.                                            |
| @Invex.FileSystem.TransformFileScope                          | Temporary, reversible single-file transformation.                                    |
| @Invex.FileSystem.TransformMultiFileScope                     | Temporary, reversible multi-file transformation.                                     |
| @Invex.FileSystem.TransformFileScopeExtensions                | Fluent `AddAsync` chaining for transform scope tasks.                                |
| @Invex.FileSystem.FileSystemHostExtensions                    | DI registration: `AddRootedFileSystem` and `ProvidePath`.                            |

For conceptual documentation and usage examples, see the [Docs](../docs/introduction.md) section.

