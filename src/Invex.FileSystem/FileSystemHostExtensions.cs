namespace Invex.FileSystem;

/// <summary>
///     Provides extension methods for registering the file system services with an
///     <see cref="IServiceCollection" />.
/// </summary>
[PublicAPI]
public static class FileSystemHostExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        ///     Registers the file system services with the dependency injection container.
        /// </summary>
        /// <returns>The same <see cref="IServiceCollection" /> for further configuration.</returns>
        /// <remarks>
        ///     <para>Registers the following services:</para>
        ///     <list type="bullet">
        ///         <item>
        ///             A keyed singleton <c>"RootedFileSystem"</c> of type <see cref="IFileSystem" />
        ///             backed by the real <see cref="System.IO.Abstractions.FileSystem" />.  This
        ///             keyed registration exists so that the inner file system can be resolved
        ///             independently of the outer <see cref="IRootedFileSystem" /> wrapper if needed.
        ///         </item>
        ///         <item>
        ///             A singleton <see cref="IRootedFileSystem" /> whose <see cref="IPathProvider" />
        ///             list is populated from all <see cref="IPathProvider" /> services registered in
        ///             the container, sorted by descending <see cref="IPathProvider.Priority" />.
        ///         </item>
        ///         <item>
        ///             A singleton <see cref="IFileSystem" /> (non-keyed) that resolves to the same
        ///             instance as <see cref="IRootedFileSystem" />, so code that depends on the base
        ///             abstraction automatically gets the rooted file system implementation.
        ///         </item>
        ///     </list>
        ///     Call <c>ProvidePath</c> after this method to register additional path providers.
        /// </remarks>
        public IServiceCollection AddRootedFileSystem() =>
            serviceCollection
                .AddKeyedSingleton<IFileSystem>("RootedFileSystem", new System.IO.Abstractions.FileSystem())
                .AddSingleton<IRootedFileSystem>(services =>
                    new RootedFileSystem(services.GetRequiredService<ILogger<RootedFileSystem>>())
                    {
                        FileSystem = services.GetRequiredKeyedService<IFileSystem>("RootedFileSystem"),
                        PathProviders = services
                            .GetServices<IPathProvider>()
                            .OrderByDescending(l => l.Priority)
                            .ToList(),
                    })
                .AddSingleton<IFileSystem>(services => services.GetRequiredService<IRootedFileSystem>());

        /// <summary>
        ///     Registers a custom path provider using a simple key-to-path delegate.
        /// </summary>
        /// <param name="locate">
        ///     A function that receives a path key and returns a <see cref="RootedPath" /> when it
        ///     recognises the key, or <c>null</c> to defer to the next provider.
        /// </param>
        /// <param name="priority">
        ///     The priority of the provider.  Higher values are queried before lower values.
        ///     Defaults to <c>1</c>, which is above the built-in provider at priority <c>0</c>.
        /// </param>
        /// <remarks>
        ///     Use the overload that also accepts an <see cref="IRootedFileSystem" /> parameter when
        ///     the path logic needs to call <see cref="IRootedFileSystem.GetPath" /> for another key
        ///     (e.g. to build a path relative to the project root) — doing so through a closure over
        ///     the raw <see cref="IServiceProvider" /> would risk a circular dependency.
        /// </remarks>
        [PublicAPI]
        public void ProvidePath(Func<string, RootedPath?> locate, int priority = 1) =>
            serviceCollection.AddSingleton<IPathProvider>(new FunctionPathProvider
            {
                Priority = priority,
                Provider = locate,
            });

        /// <summary>
        ///     Registers a custom path provider using a delegate that also receives the current
        ///     <see cref="IRootedFileSystem" /> instance.
        /// </summary>
        /// <param name="locate">
        ///     A function that receives a path key and the resolved <see cref="IRootedFileSystem" />
        ///     and returns a <see cref="RootedPath" /> when it recognises the key, or <c>null</c> to
        ///     defer to the next provider.
        /// </param>
        /// <param name="priority">
        ///     The priority of the provider.  Higher values are queried before lower values.
        ///     Defaults to <c>1</c>.
        /// </param>
        /// <remarks>
        ///     Prefer this overload when the path depends on another resolved path, for example:
        ///     <code>
        /// services.ProvidePath((key, fs) => key == "MyOutput"
        ///     ? fs.GetPath("Root") / "output"
        ///     : null);
        ///     </code>
        ///     The <see cref="IRootedFileSystem" /> is resolved lazily from the
        ///     <see cref="IServiceProvider" /> when the provider is first invoked, avoiding
        ///     circular-construction issues at startup.
        /// </remarks>
        [PublicAPI]
        public void ProvidePath(Func<string, IRootedFileSystem, RootedPath?> locate, int priority = 1) =>
            serviceCollection.AddSingleton<IPathProvider>(provider => new FunctionPathProvider
            {
                Priority = priority,
                Provider = key => locate(key, provider.GetRequiredService<IRootedFileSystem>()),
            });
    }
}
