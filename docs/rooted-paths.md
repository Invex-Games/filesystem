# Rooted Paths

`RootedPath` is the core value type of the library: a `record` that binds an **absolute path string** to the
`IFileSystem` it belongs to. Because the file system travels with the path, existence checks, queries, and composition
need no extra context.

```csharp
public record RootedPath(IFileSystem FileSystem, string Path)
```

## Creating a rooted path

```csharp
// From the rooted file system (preferred — binds to IRootedFileSystem):
RootedPath root = fs.GetPath("Root");
RootedPath current = fs.CurrentDirectory;
RootedPath custom = fs.CreateRootedPath(@"C:\data");

// Directly, over any IFileSystem:
var path = new RootedPath(new FileSystem(), @"C:\data");
```

> [!TIP]
> When working with an `IRootedFileSystem`, prefer `CreateRootedPath` over the constructor so the resulting path is
> bound to the rooted wrapper rather than the inner file system.

## Composing paths

Use the `/` operator to append segments. Each application returns a *new* `RootedPath` bound to the same file system:

```csharp
RootedPath projectFile = fs.GetPath("Root") / "src" / "App" / "App.csproj";
```

The operator delegates to `IPath.Combine`, so platform-appropriate separators are used automatically.

## Querying paths

| Member                     | Returns          | Behaviour                                                                  |
|----------------------------|------------------|-----------------------------------------------------------------------------|
| `PathExists`               | `bool`           | `true` if the path exists as a file **or** a directory.                     |
| `FileExists`               | `bool`           | `true` if the path exists as a file.                                        |
| `DirectoryExists`          | `bool`           | `true` if the path exists as a directory.                                   |
| `FileName`                 | `string?`        | File name with extension; `null` if the path is not an existing file.       |
| `FileNameWithoutExtension` | `string`         | Pure string operation — always returns a value, no existence check.         |
| `DirectoryName`            | `string?`        | Containing directory's path; `null` if the path is not an existing directory. |
| `Parent`                   | `RootedPath?`    | The parent directory; `null` when the path is already a root.               |

> [!NOTE]
> Members that depend on the entry *type* (`FileName`, `DirectoryName`) intentionally return `null` instead of
> throwing when the path doesn't match, so you can use null-coalescing patterns naturally:
>
> ```csharp
> string name = path.FileName ?? "(not a file)";
> ```

### `Parent`

`Parent` strips any trailing separator before computing the parent, so `/foo/` and `/foo` both yield `/`. The returned
path always ends with a directory separator, so it can be combined further with `/` without producing double
separators:

```csharp
RootedPath? parent = (fs.CreateRootedPath(@"C:\a\b\c")).Parent;  // C:\a\b\
RootedPath sibling = parent! / "d";                               // C:\a\b\d
```

## Using paths with standard APIs

`RootedPath` implicitly converts to `string`, and `ToString()` returns the raw path, so it can be handed to anything
that takes a path:

```csharp
RootedPath path = fs.GetPath("Root") / "notes.txt";

File.ReadAllText(path);              // implicit string conversion
Console.WriteLine($"Path: {path}");  // ToString()
```

## Value semantics

As a `record`, `RootedPath` has structural equality — two instances are equal when they have the same `FileSystem`
reference and `Path` string. Use `with` expressions to derive new paths:

```csharp
RootedPath renamed = path with { Path = path.Path.Replace(".txt", ".md") };
```

