# Path Providers

Path resolution is decoupled from file I/O. `IRootedFileSystem.GetPath(string)` queries every registered
`IPathProvider` in **descending priority order** and returns the first non-`null` result, caching it for subsequent
calls.

```
GetPath("Artifacts")
   │
   ├─► Provider (priority 5)  → null        (doesn't recognise the key — skipped)
   ├─► Provider (priority 1)  → RootedPath  (first non-null result wins)
   └─► Provider (priority 0)                (never reached for this key)
```

## Registering providers inline

The `ProvidePath` extension methods register a delegate-backed provider (`FunctionPathProvider`) without needing a
dedicated class:

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

> [!IMPORTANT]
> Use the `(key, fs)` overload whenever the path depends on **another resolved path**. The `IRootedFileSystem` is
> resolved lazily when the provider is first invoked, which avoids circular-construction problems at startup.
> Capturing the raw `IServiceProvider` in a closure and resolving `IRootedFileSystem` yourself risks a circular
> dependency.

## Priorities and ordering

- **Higher values are queried first.** When two providers can both resolve the same key, the one with the higher
  priority wins.
- **Equal priorities are queried in registration order** (the sort is stable).
- **Use a negative priority** for a low-priority fallback that only applies when no other provider matches.
- A provider that does not recognize a key must return `null` to pass resolution to the next provider in the chain.

## Caching

Results are cached per key after the first successful resolution. Repeated calls to `GetPath("Root")` return the same
`RootedPath` instance without re-querying providers — so each provider's `GetPath` is called at most once per key per
`IRootedFileSystem` lifetime.

## Custom provider implementations

For complex resolution logic — searching the file system, reading configuration, combining multiple keys — implement
`IPathProvider` directly and register it as a service:

```csharp
public sealed class GitRootPathProvider(IFileSystem fileSystem) : IPathProvider
{
    public int Priority => 10;

    public RootedPath? GetPath(string key)
    {
        if (key != "GitRoot")
            return null;

        var dir = new RootedPath(fileSystem, fileSystem.Directory.GetCurrentDirectory());

        while (dir is not null && !(dir / ".git").DirectoryExists)
            dir = dir.Parent;

        return dir;
    }
}
```

```csharp
services.AddSingleton<IPathProvider, GitRootPathProvider>();
services.AddRootedFileSystem();
```

> [!NOTE]
> Registration order relative to `AddRootedFileSystem()` does not matter — providers are collected from the container
> when `IRootedFileSystem` is first resolved.

## Error handling

If no provider recognizes a key, `GetPath` throws `InvalidOperationException`:

```
Could not locate path for key 'Artifcats'
```

Providers may call `GetPath` for *other* keys while resolving their own (as in the `"Output"` example above). A
recursion-depth guard throws `InvalidOperationException` if resolution nests more than 100 levels deep, which almost
always indicates a circular key dependency such as `"A" → "B" → "A"`.

