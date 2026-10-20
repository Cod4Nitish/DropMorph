// The conversion pipeline without the app: the user's settings and presets, every engine (bundled ones next to the
// executable, downloaded ones in the per-user engines folder) and a job queue. Mirrors AppHost in DropMorph.App.

using DropMorph.Core.Conversion;
using DropMorph.Core.Localization;
using DropMorph.Core.Settings;
using DropMorph.Engines;
using DropMorph.Engines.Infrastructure;

namespace DropMorph.Cli;

internal sealed class CliHost : IAsyncDisposable
{
    private CliHost(UserDataStore store, ConverterCatalog catalog, JobQueue queue)
    {
        Store = store;
        Catalog = catalog;
        Queue = queue;
    }

    public UserDataStore Store { get; }
    public ConverterCatalog Catalog { get; }
    public JobQueue Queue { get; }
    public ILocalizer Texts { get; } = new EnglishTexts();

    /// <param name="dataDirectory">Settings folder; the app's (%APPDATA%\DropMorph) when null.</param>
    public static CliHost Create(string? dataDirectory = null)
    {
        var store = new UserDataStore(dataDirectory ?? UserDataStore.DefaultDirectory);
        store.Load();
        EngineEnvironment.OwnCopies = new Dictionary<string, string>(store.Settings.EngineOwnCopies);
        EngineEnvironment.LibreOfficeForSlides = store.Settings.LibreOfficeForSlides;
        EngineEnvironment.LibreOfficeForHwp = store.Settings.LibreOfficeForHwp;
        EngineEnvironment.LibreOfficeForDocuments = store.Settings.LibreOfficeForDocuments;
        EngineEnvironment.LibreOfficeForSheets = store.Settings.LibreOfficeForSheets;
        var converters = EngineRegistry.CreateAll(new EngineEnvironment(store.Directory));
        var catalog = new ConverterCatalog(converters) { Priority = store.Settings.EnginePriority };
        var queue = new JobQueue(catalog, EngineRegistry.FindPdfMerger(converters), combiner: EngineRegistry.FindFileCombiner(converters),
            tiffMerger: EngineRegistry.FindTiffMerger(converters));
        return new CliHost(store, catalog, queue);
    }

    public ValueTask DisposeAsync() => Queue.DisposeAsync();
}
