# Testing

Because the library builds on `System.IO.Abstractions`, everything can be exercised against an in-memory
`MockFileSystem` — no real disk required.

```shell
dotnet add package TestableIO.System.IO.Abstractions.TestingHelpers
```

## Testing with `RootedPath` directly

Construct a `RootedPath` over a `MockFileSystem` and use it exactly like a real path:

```csharp
using System.IO.Abstractions.TestingHelpers;

var mock = new MockFileSystem();
var path = new RootedPath(mock, "/repo/file.txt");

mock.AddFile(path, new MockFileData("hello"));

Assert.True(path.FileExists);
Assert.Equal("file.txt", path.FileName);
```

## Testing code that depends on `IRootedFileSystem`

Wire up the DI container with a mock file system by overriding the keyed backing registration:

```csharp
var mock = new MockFileSystem(new Dictionary<string, MockFileData>
{
    ["/repo/src/App/App.csproj"] = new("<Project />"),
}, "/repo");

var services = new ServiceCollection();
services.AddLogging();
services.AddRootedFileSystem();

// Replace the real file system with the mock.
services.AddKeyedSingleton<IFileSystem>("RootedFileSystem", mock);

services.ProvidePath((key, fs) => key switch
{
    "Root" => fs.CurrentDirectory,
    _      => null,
});

using var provider = services.BuildServiceProvider();
var fs = provider.GetRequiredService<IRootedFileSystem>();

RootedPath csproj = fs.GetPath("Root") / "src" / "App" / "App.csproj";
Assert.True(csproj.FileExists);
```

> [!NOTE]
> `MockFileSystem` lets you set the current directory (the second constructor argument above), which makes
> `IRootedFileSystem.CurrentDirectory` fully deterministic in tests.

## Testing transform scopes

Transform scopes work over whatever file system the `RootedPath` carries, so rollback behaviour is easy to verify:

```csharp
var mock = new MockFileSystem();
var file = new RootedPath(mock, "/config.json");
mock.AddFile(file, new MockFileData("""{ "version": "1.0.0" }"""));

await using (await TransformFileScope.CreateAsync(file, c => c.Replace("1.0.0", "2.0.0")))
{
    Assert.Contains("2.0.0", mock.File.ReadAllText(file));
}

// Restored after dispose.
Assert.Contains("1.0.0", mock.File.ReadAllText(file));
```

## Faking path resolution in unit tests

For code that takes `IRootedFileSystem`, you rarely need a full container — a provider registered inline is often
enough, or you can substitute the interface entirely with your mocking library of choice (the interface's I/O members
have default implementations that delegate to the `FileSystem` property, so only `FileSystem` and `GetPath` need
stubbing).

