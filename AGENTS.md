# Copilot Instructions

Guidance for AI agents working in **Invex.FileSystem** — a strongly-typed file system library for
.NET that layers keyed path resolution and reversible file transformations on top of
[`System.IO.Abstractions`](https://github.com/TestableIO/System.IO.Abstractions). Keep changes
focused and defer to the linked docs for detail.

## What's in the repo

| Project                  | Role                                                                          | Target frameworks       |
|--------------------------|-------------------------------------------------------------------------------|-------------------------|
| `Invex.FileSystem`       | The library: `IRootedFileSystem`, `RootedPath`, path providers/markers, transform scopes | `net10.0;net9.0;net8.0` |
| `Invex.FileSystem.Tests` | NUnit test suite, including a public API surface snapshot test                | `net10.0;net9.0;net8.0` |

Sources live under `src/`, tests under `tests/`, the Atom build definition under `_atom/`, and the
DocFX documentation site at the repo root (`docfx.json`, `index.md`, `docs/`, `api/`).

## Build & language specifics

- **.NET 10 SDK** is required. The library and test projects multi-target `net10.0;net9.0;net8.0`.
- C# **LangVersion 14**, `ImplicitUsings` and `Nullable` enabled, `TreatWarningsAsErrors` on.
- Global usings live in each project's `_usings.cs` — add shared usings there, not per-file.
- `GenerateDocumentationFile` is on; all public members need XML doc comments.
- The source uses C# 14 **extension members** (`extension(IServiceCollection ...)` blocks in
  `FileSystemHostExtensions`) — preserve that style when extending DI registration.

Build and test the whole solution:

```shell
dotnet build Invex.FileSystem.slnx
dotnet test Invex.FileSystem.slnx
```

## Architecture overview

- **`IRootedFileSystem`** (`RootedFileSystem.cs`) — extends `IFileSystem` with keyed path
  resolution. All I/O members have default interface implementations that delegate to the
  `FileSystem` property. `GetPath(string)` queries the `IPathProvider` chain in descending
  priority order, returns the first non-`null` result, and caches it per key. An `AsyncLocal`
  depth counter (limit 100) guards against circular key dependencies. The concrete
  `RootedFileSystem` class is `internal`; consumers only see the interface.
- **`RootedPath`** (`RootedPath.cs`) — a record pairing an `IFileSystem` with an absolute path
  string. Composes with the `/` operator, implicitly converts to `string`, and its
  file/directory-specific members (`FileName`, `DirectoryName`) deliberately return `null`
  instead of throwing when the entry type doesn't match.
- **`IPathProvider` / `FunctionPathProvider`** — resolve a `RootedPath` for a key, or return
  `null` to defer to the next provider. Higher `Priority` wins; ties break by registration order
  (stable sort in `AddRootedFileSystem`).
- **`IPathMarker`** — strongly-typed path via a `static abstract` `Path(IFileSystem)` method;
  `GetPath<T>()` bypasses the provider chain and cache entirely.
- **`TransformFileScope` / `TransformMultiFileScope`** — disposable scopes that capture file
  content, apply a transform, and restore the original on dispose (or delete files the scope
  created). `CancelRestore()` commits instead. Both have sync and async create/add/dispose paths
  that must stay behaviorally identical — if you change one, mirror the other.
- **`FileSystemHostExtensions`** — DI wiring: `AddRootedFileSystem()` registers a keyed
  `IFileSystem` (`"RootedFileSystem"`) backing instance, the `IRootedFileSystem` singleton, and a
  non-keyed `IFileSystem` aliased to it. `ProvidePath(...)` registers delegate-backed providers.

## Key design rules

- **Everything goes through `IFileSystem`** — never call `System.IO` directly in `src/`
  (the `TestableIO.System.IO.Abstractions.Analyzers` package enforces this). This keeps the
  whole library testable against `MockFileSystem`.
- `RootedPath` operations must preserve the `FileSystem` reference they were created with
  (use `with` expressions, as the `/` operator does).
- Path resolution stays separate from file I/O: new "where is X?" logic belongs in a provider or
  marker, not in ad-hoc path computation at call sites.
- The `(key, fs)` `ProvidePath` overload resolves `IRootedFileSystem` lazily to avoid circular
  construction — don't "simplify" it into an eager resolution.
- Transform scope disposal must never accept a cancellation token: restores always run to
  completion so files are never left partially written.

## Atom workflows

The GitHub Actions workflow YAML under `.github/workflows/` (`Validate.yml`, `Build.yml`,
`Dependabot Enable auto-merge.yml`, `Cleanup Prereleases.yml`) and `.github/dependabot.yml` are
**generated** from the Atom build definition in `_atom/IBuild.cs`.

Whenever you change anything that affects the workflows — targets, workflow definitions,
triggers, options, or params/secrets — regenerate the YAML:

```shell
atom gen
```

(equivalently `dotnet run --project _atom -- gen`). Commit the regenerated `.github/` files
alongside your `_atom/` changes; never hand-edit the generated YAML. A drift between
`_atom/IBuild.cs` and the committed YAML should be treated as a missing `atom gen` run.

Docs are built and published by Atom targets too (`BuildDocs`, `ServeDocs`, `PublishDocs`) — the
site deploys to GitHub Pages on stable releases.

## Conventions

- Annotate every new public type with `[PublicAPI]` — the in-repo analyzer flags anything
  missing, and warnings are errors.
- Add XML doc comments to all public types and members. Match the existing `<summary>` /
  `<param>` / `<remarks>` style: documented `null`-return semantics, caching behavior, and
  cancellation behavior are part of the contract.
- Use **Conventional Commits** — the prefix drives versioning:

  | Prefix                          | Version bump |
  |---------------------------------|--------------|
  | `breaking:` / `major:`          | Major        |
  | `feat:` / `feature:` / `minor:` | Minor        |
  | `fix:` / `patch:`               | Patch        |
  | `semver-none` / `semver-skip`   | No bump      |

- When adding user-facing features, update the relevant `docs/` page and `README.md`.

## Testing & the Verify workflow

- Tests use **NUnit** with **Shouldly**, **FakeItEasy**, **MockFileSystem**
  (`TestableIO.System.IO.Abstractions.TestingHelpers`), and **Verify** (`Verify.NUnit`) for
  snapshot/approval testing.
- Never touch the real disk in tests — use `MockFileSystem`.
- A snapshot test fails when its output differs from the committed `*.verified.txt`. On failure,
  Verify writes a `*.received.txt` next to it. If the diff is unintended, fix the code. If the
  change is valid:
  1. Overwrite the `*.verified.txt` with the contents of the matching `*.received.txt`.
  2. Delete the `*.received.txt`.
  3. Re-run `dotnet test` to confirm the suite is green.
- `PublicApiSurfaceTests` snapshots the complete public API into
  `PublicApiTests.VerifyPublicApiSurface.verified.txt`. An unexpected diff there signals an
  unintentional API change — treat it as such and double-check before accepting. The Validate
  workflow's `CheckPrForBreakingChanges` target also inspects `*.verified.txt` diffs on PRs to
  flag breaking changes.

## Common tasks

### Adding a member to `RootedPath` or `IRootedFileSystem`

1. Implement it via `IFileSystem` members only, preserving the `FileSystem` binding.
2. Add `[PublicAPI]`-covered XML docs (document `null` semantics if applicable).
3. Add unit tests against `MockFileSystem`.
4. Accept the public API surface snapshot change (see Verify workflow above).
5. Update `docs/rooted-paths.md` (and `README.md` if user-facing).

### Changing transform scope behaviour

1. Apply the change to **both** the sync and async variants, and to both
   `TransformFileScope` and `TransformMultiFileScope` where applicable.
2. Keep the restore guarantees intact (original content restored; created files deleted;
   dispose idempotent; no cancellation during restore).
3. Update the behaviour-reference table in `docs/transform-scopes.md`.

## Defer to the docs

For anything beyond the above, prefer these over duplicating detail:

- `README.md` — package overview and quick start.
- `docs/introduction.md` — what the library is and why it exists.
- `docs/getting-started.md` — installation and DI wiring.
- `docs/rooted-paths.md` — `RootedPath` in depth.
- `docs/path-providers.md` — provider chain, priorities, caching, custom providers.
- `docs/path-markers.md` — `IPathMarker` and when to use it.
- `docs/transform-scopes.md` — transform scopes and their behaviour reference.
- `docs/testing.md` — testing with `MockFileSystem`.

