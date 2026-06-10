# Path Markers

`IPathMarker` gives a **compile-time name** to a path that is statically determined by a type, rather than resolved
through the provider chain.

```csharp
public interface IPathMarker
{
    static abstract RootedPath Path(IFileSystem fileSystem);
}
```

## When to use markers vs. keys

| Aspect           | Keyed lookup (`GetPath("X")`)                  | Marker lookup (`GetPath<TMarker>()`)              |
|------------------|------------------------------------------------|----------------------------------------------------|
| Identified by    | A string key                                   | A type                                             |
| Resolved by      | The registered `IPathProvider` chain           | The marker type's own static `Path` method         |
| Cached?          | Yes — once per key                             | No — computed on every call                        |
| Typo safety      | Runtime failure                                | Compile-time failure                               |
| Best for         | Locations configured per-application            | Locations intrinsic to a type (e.g. source-generated project paths) |

## Defining a marker

```csharp
public sealed class ArtifactsPath : IPathMarker
{
    public static RootedPath Path(IFileSystem fileSystem) =>
        ((IRootedFileSystem)fileSystem).GetPath("Root") / "artifacts";
}
```

## Resolving a marker

```csharp
RootedPath artifacts = fs.GetPath<ArtifactsPath>();
```

`GetPath<T>()` simply invokes `T.Path(this)` — it bypasses the provider chain and the cache entirely. Because the
current `IRootedFileSystem` is passed in, the returned `RootedPath` stays consistent with every other path resolved by
the application (and with mock file systems in tests).

> [!NOTE]
> Markers use C#'s *static abstract interface members*, so they require .NET 8+ and cannot be instantiated — the
> marker type exists purely to carry the path computation.

## Combining markers with providers

Markers and providers compose naturally — a marker can build on keyed lookups, as in the example above, and a provider
can return a marker-derived path:

```csharp
services.ProvidePath((key, fs) => key == "PackageOutput"
    ? fs.GetPath<ArtifactsPath>() / "packages"
    : null);
```

