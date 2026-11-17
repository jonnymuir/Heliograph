using Heliograph.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Heliograph.Api;

/// <summary>Registers what every Heliograph host needs: the library of operations, over a store and a dictionary source.</summary>
public static class HeliographServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CorrespondenceLibrary"/> over the <see cref="ICorrespondenceStore"/> and
    /// <see cref="IDictionarySource"/> the host has registered. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddCorrespondenceLibrary(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(provider => new CorrespondenceLibrary(
            provider.GetRequiredService<ICorrespondenceStore>(),
            provider.GetRequiredService<IDictionarySource>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System));

        // Edit links are signed with the host's Data Protection keys. A site with several servers shares its key ring,
        // as it already must for sign-in cookies.
        services.AddDataProtection();
        services.TryAddSingleton(provider => new EditLinks(
            provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System));
        return services;
    }

    /// <summary>
    /// The smallest whole setup: correspondence and dictionaries kept as ordinary files under one folder
    /// (<c>correspondence/</c> and <c>dictionaries/</c>), and the library over them. Right for a reference app or a
    /// small site on one server.
    /// </summary>
    public static IServiceCollection AddHeliographInFolder(this IServiceCollection services, string folder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        services.TryAddSingleton<ICorrespondenceStore>(new FolderCorrespondenceStore(Path.Combine(folder, "correspondence")));
        services.TryAddSingleton<IDictionarySource>(new FolderDictionarySource(Path.Combine(folder, "dictionaries")));
        return services.AddCorrespondenceLibrary();
    }
}
