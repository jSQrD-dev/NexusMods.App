﻿using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.FileStore;
using NexusMods.Abstractions.FileStore.ArchiveMetadata;
using NexusMods.Abstractions.GameLocators;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Games.Loadouts;
using NexusMods.Abstractions.Installers;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Abstractions.Loadouts.Ids;
using NexusMods.Abstractions.Loadouts.Mods;
using NexusMods.Abstractions.Serialization;
using NexusMods.App.UI;
using NexusMods.DataModel.Loadouts;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Paths;
using NexusMods.StandardGameLocators.TestHelpers.StubbedGames;

namespace NexusMods.UI.Tests;

public class AVmTest<TVm> : AUiTest, IAsyncLifetime
where TVm : IViewModelInterface
{
    protected AbsolutePath DataZipLzma => FileSystem.GetKnownPath(KnownPath.EntryDirectory).Combine("Resources/data_zip_lzma.zip");
    protected AbsolutePath Data7ZLzma2 => FileSystem.GetKnownPath(KnownPath.EntryDirectory).Combine("Resources/data_7zip_lzma2.7z");

    protected AbsolutePath DataTest =>
        FileSystem.GetKnownPath(KnownPath.EntryDirectory).Combine("Resources/data.test");

    private VMWrapper<TVm> _vmWrapper { get; }
    protected StubbedGame Game { get; }
    protected IFileSystem FileSystem { get; }
    protected GameInstallation Install { get; }
    protected IConnection Connection { get; }

    protected IDataStore DataStore { get; }
    protected IArchiveInstaller ArchiveInstaller { get; }

    protected IFileOriginRegistry FileOriginRegistry { get; }

    private Loadout.Model? _loadout;
    protected Loadout.Model Loadout => _loadout!;

    public AVmTest(IServiceProvider provider) : base(provider)
    {
        _vmWrapper = GetActivatedViewModel<TVm>();
        DataStore = provider.GetRequiredService<IDataStore>();
        Connection = provider.GetRequiredService<IConnection>();
        Game = provider.GetRequiredService<StubbedGame>();
        Install = Game.Installations.First();
        FileSystem = provider.GetRequiredService<IFileSystem>();
        ArchiveInstaller = provider.GetRequiredService<IArchiveInstaller>();
        FileOriginRegistry = provider.GetRequiredService<IFileOriginRegistry>();
    }

    protected TVm Vm => _vmWrapper.VM;

    public async Task InitializeAsync()
    {
        _loadout = await ((IGame)Install.Game).Synchronizer.Manage(Install, "Test");
    }

    protected async Task<ModId[]> InstallMod(AbsolutePath path)
    {
        var downloadId = await FileOriginRegistry.RegisterDownload(path,
            (tx, id) =>
            {
                tx.Add(id, FilePathMetadata.OriginalName, path.FileName);
            });
        return await ArchiveInstaller.AddMods(Loadout.LoadoutId, downloadId);
    }

    public Task DisposeAsync()
    {
        _vmWrapper.Dispose();
        return Task.CompletedTask;
    }
}

public class AVmTest<TVm, TVmInterface> : AVmTest<TVmInterface> where TVmInterface : IViewModelInterface
where TVm : TVmInterface
{
    public AVmTest(IServiceProvider provider) : base(provider) { }

    /// <summary>
    /// The concrete view model, not the interface.
    /// </summary>
    public TVm ConcreteVm => (TVm) Vm;
}
