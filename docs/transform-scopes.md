# File Transform Scopes

`TransformFileScope` and `TransformMultiFileScope` apply **temporary, reversible edits** to files. On creation the
original content is captured and the transform is written immediately; on disposal the original content is restored —
unless you commit the change with `CancelRestore()`.

This is ideal for operations that must temporarily patch a file — injecting a version number into a project file,
toggling a feature flag in a config file — and must guarantee the file is returned to its original state even when the
operation fails partway through.

## Single file

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

Synchronous variants exist for non-async contexts:

```csharp
using var scope = TransformFileScope.Create(projectFile, c => c.Replace("1.0.0", "2.0.0"));
```

## Multiple files

`TransformMultiFileScope` applies the same transform to a set of files, processing them **in parallel** (async) and
restoring all of them on dispose:

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

## Chaining additional transforms

Both scope types support `Add` / `AddAsync` to layer further transformations onto an open scope, and extension methods
allow the whole pipeline to be written as a single fluent expression:

```csharp
await using var scope = await TransformFileScope
    .CreateAsync(projectFile, _ => "version=1")
    .AddAsync(content => content + "\nbuild=ci");
```

> [!NOTE]
> No matter how many `Add` calls are made, disposal restores the **original** content captured when the scope was
> created — the transformations are unwound all at once, not one by one.

> [!TIP]
> The fluent `Task<...>.AddAsync(...)` extensions do not forward a `CancellationToken`. If you need cancellation
> support for the additional transform, `await` the scope into a local and call its `AddAsync` overload directly.

## Committing instead of rolling back

Call `CancelRestore()` to keep the transformed content permanently:

```csharp
await using var scope = await TransformFileScope.CreateAsync(file, c => Patch(c));

RunBuildStep();

scope.CancelRestore(); // success — commit the change; dispose becomes a no-op
```

Once `CancelRestore()` has been called it cannot be undone, and subsequent `Add` / `AddAsync` calls are skipped.

## Behaviour reference

| Situation                                   | Behaviour                                                                  |
|---------------------------------------------|------------------------------------------------------------------------------|
| File does not exist at creation             | The file is created; the transform receives an empty string; disposal **deletes** the file. |
| `CancellationToken` cancelled mid-write     | The file(s) are restored, then `OperationCanceledException` propagates.       |
| `Add` / `AddAsync` after dispose            | Throws `ObjectDisposedException`.                                             |
| `Add` / `AddAsync` after `CancelRestore`    | No-op; the scope is returned unchanged.                                       |
| Dispose called multiple times               | Safe — subsequent calls are no-ops.                                           |
| Sync `Dispose` on the multi-file scope      | Restores files sequentially; prefer `await using` for parallel restore.       |

> [!IMPORTANT]
> Disposal intentionally accepts no cancellation token: restoring content always runs to completion so a file is never
> left in a partially-written state.

