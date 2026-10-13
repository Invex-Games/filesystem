# API Reference

The generated reference for every public type in the `Invex.FileSystem` namespace; the table below is a map of where
to start.

## Invex.FileSystem

| Type                                           | Kind         | Description                                                                         |
|------------------------------------------------|--------------|-------------------------------------------------------------------------------------|
| @Invex.FileSystem.IRootedFileSystem            | Interface    | File system abstraction with `GetPath`, `CurrentDirectory`, and `CreateRootedPath`. |
| @Invex.FileSystem.RootedPath                   | Record       | A path bound to an `IFileSystem`, with composition and existence helpers.           |
| @Invex.FileSystem.IPathProvider                | Interface    | Resolves a `RootedPath` for a key; queried by priority.                             |
| @Invex.FileSystem.FunctionPathProvider         | Class        | Delegate-based `IPathProvider` implementation.                                      |
| @Invex.FileSystem.IPathMarker                  | Interface    | Strongly-typed, statically-computed path.                                           |
| @Invex.FileSystem.TransformFileScope           | Class        | Temporary, reversible single-file transformation.                                   |
| @Invex.FileSystem.TransformMultiFileScope      | Class        | Temporary, reversible multi-file transformation.                                    |
| @Invex.FileSystem.TransformFileScopeExtensions | Static class | Fluent `AddAsync` chaining for transform scope tasks.                               |
| @Invex.FileSystem.FileSystemHostExtensions     | Static class | DI registration: `AddRootedFileSystem` and `ProvidePath`.                           |

## Typical flow

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddRootedFileSystem();
services.ProvidePath((key, fs) => key == "Root" ? fs.CurrentDirectory : null);

using var provider = services.BuildServiceProvider();
var fs = provider.GetRequiredService<IRootedFileSystem>();
RootedPath props = fs.GetPath("Root") / "Directory.Build.props";

await using var scope = await TransformFileScope.CreateAsync(props, c => c.Replace("1.0.0", "2.0.0"));
```

For conceptual documentation and usage examples, see the [Introduction](../docs/introduction.md).
