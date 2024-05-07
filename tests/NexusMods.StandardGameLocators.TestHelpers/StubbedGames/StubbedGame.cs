using Microsoft.Extensions.Logging;
using NexusMods.Abstractions.DiskState;
using NexusMods.Abstractions.GameLocators;
using NexusMods.Abstractions.GameLocators.GameCapabilities;
using NexusMods.Abstractions.GameLocators.Stores.EADesktop;
using NexusMods.Abstractions.GameLocators.Stores.EGS;
using NexusMods.Abstractions.GameLocators.Stores.GOG;
using NexusMods.Abstractions.GameLocators.Stores.Origin;
using NexusMods.Abstractions.GameLocators.Stores.Steam;
using NexusMods.Abstractions.GameLocators.Stores.Xbox;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Games.DTO;
using NexusMods.Abstractions.Games.Loadouts;
using NexusMods.Abstractions.Installers;
using NexusMods.Abstractions.IO;
using NexusMods.Abstractions.IO.StreamFactories;
using NexusMods.Abstractions.Loadouts.Mods;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Abstractions.Serialization;
using NexusMods.Hashing.xxHash64;
using NexusMods.Paths;
using NexusMods.Paths.Extensions;
using File = NexusMods.Abstractions.Loadouts.Files.File;

// ReSharper disable InconsistentNaming

namespace NexusMods.StandardGameLocators.TestHelpers.StubbedGames;

public class StubbedGame : AGame, IEADesktopGame, IEpicGame, IOriginGame, ISteamGame, IGogGame, IXboxGame
{
    private readonly ILogger<StubbedGame> _logger;
    private readonly IEnumerable<IGameLocator> _locators;
    public override string Name => "Stubbed Game";
    public override GameDomain Domain => GameDomain.From("stubbed-game");

    public static readonly RelativePath[] DATA_NAMES = new[]
    {
        "StubbedGame.exe",
        "config.ini",
        "Data/image.dds",
        "Models/model.3ds"
    }.Select(t => t.ToRelativePath()).ToArray();

    public static readonly Dictionary<RelativePath, (Hash Hash, Size Size)> DATA_CONTENTS = DATA_NAMES
        .ToDictionary(d => d,
            d => (d.FileName.ToString().XxHash64AsUtf8(), Size.FromLong(d.FileName.ToString().Length)));

    private readonly IFileSystem _fileSystem;
    private Dictionary<AbsolutePath, DateTime> _modifiedTimes = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<GameStore, Dictionary<LocationId, AbsolutePath>> _locations = new();
    private bool _initalized;

    public StubbedGame(ILogger<StubbedGame> logger, IEnumerable<IGameLocator> locators,
        IFileSystem fileSystem, IServiceProvider provider) : base(provider)
    {
        _serviceProvider = provider;
        _logger = logger;
        _locators = locators;
        _fileSystem = fileSystem;
        _initalized = false;
    }

    public override GamePath GetPrimaryFile(GameStore store) => new(LocationId.Game, "");

    public void ResetGameFolders()
    {
        // Re-create the folders/files
        foreach (var locator in _locators)
        {
            foreach (var result in locator.Find(this))
            {
                if (result.Path.DirectoryExists())
                    result.Path.DeleteDirectory(true);
                var locations = new Dictionary<LocationId, AbsolutePath>
                {
                    [LocationId.Game] = EnsureFiles(result.Path, LocationId.Game),
                    [LocationId.Preferences] = EnsurePath(result.Path, LocationId.Preferences),
                    [LocationId.Saves] = EnsurePath(result.Path, LocationId.Saves)
                };
                _locations[result.Store] = locations;
            }
        }
    }

    public override IEnumerable<GameInstallation> Installations
    {
        get
        {
            if (!_initalized)
            {
                ResetGameFolders();
                _initalized = true;
            }

            _logger.LogInformation("Looking for {Game} in {Count} locators", ToString(), _locators.Count());
            return _locators.SelectMany(l => l.Find(this))
                .Select((i, idx) => new GameInstallation
                {
                    Game = this,
                    LocationsRegister = new GameLocationsRegister(_locations[i.Store]),
                    Version = Version.Parse($"0.0.{idx}.0"),
                    Store = GameStore.Unknown,
                });
        }
    }


    public override ILoadoutSynchronizer Synchronizer =>
        // Lazy initialization to avoid circular dependencies
        new StubbedGameSyncronizer(_serviceProvider, this);

    private class StubbedGameSyncronizer(IServiceProvider provider, StubbedGame thisGame) : DefaultSynchronizer(provider)
    {
        private DiskStateTree? _cachedState;
        
        public override ValueTask<(bool isCachedState, DiskStateTree tree)> GetOrCreateInitialDiskState(GameInstallation installation)
        {
            if (_cachedState != null)
                return ValueTask.FromResult((true, _cachedState));

            var results = DATA_NAMES.Select(name =>
            {
                var gamePath = new GamePath(LocationId.Game, name);
                return KeyValuePair.Create(gamePath,
                    new DiskStateEntry
                    {
                        // This is coded to match what we write in `EnsureFile`
                        Size = Size.From((ulong)name.FileName.Path.Length),
                        Hash = name.FileName.Path.XxHash64AsUtf8(),
                        LastModified = thisGame._modifiedTimes[installation.LocationsRegister.GetResolvedPath(gamePath)]
                    });
            });
            _cachedState = DiskStateTree.Create(results);
            return ValueTask.FromResult((false, _cachedState));
        }
    }

    public override IStreamFactory Icon =>
        new EmbededResourceStreamFactory<StubbedGame>(
            "NexusMods.StandardGameLocators.TestHelpers.Resources.question_mark_game.png");

    public override IStreamFactory GameImage => throw new NotImplementedException("No game image for stubbed game.");
    protected override IReadOnlyDictionary<LocationId, AbsolutePath> GetLocations(IFileSystem fileSystem,
        GameLocatorResult installation)
    {
        return new Dictionary<LocationId, AbsolutePath>()
            {
                { LocationId.Game, Installations.First().LocationsRegister[LocationId.Game] }
            };
    }

    public override List<IModInstallDestination> GetInstallDestinations(IReadOnlyDictionary<LocationId, AbsolutePath> locations) => new();

    private AbsolutePath EnsureFiles(AbsolutePath path, LocationId locationId)
    {
        lock (this)
        {
            path = path.Combine(locationId.ToString());
            path.CreateDirectory();
            foreach (var file in DATA_NAMES)
            {
                EnsureFile(path.Combine(file));
            }
            return path;
        }
    }

    private AbsolutePath EnsurePath(AbsolutePath path, LocationId locationId)
    {
        lock (this)
        {
            path = path.Combine(locationId.ToString());
            path.CreateDirectory();
            return path;
        }
    }

    private void EnsureFile(AbsolutePath path)
    {
        path.Parent.CreateDirectory();
        if (path.FileExists) return;
        _fileSystem.WriteAllText(path, path.FileName);
        _modifiedTimes[path] = DateTime.Now;
    }

    public IEnumerable<uint> SteamIds => new[] { 42u };
    public IEnumerable<long> GogIds => new[] { (long)42 };
    public IEnumerable<string> EADesktopSoftwareIDs => new[] { "ea-game-id" };
    public IEnumerable<string> EpicCatalogItemId => new[] { "epic-game-id" };
    public IEnumerable<string> OriginGameIds => new[] { "origin-game-id" };
    public IEnumerable<string> XboxIds => new[] { "xbox-game-id" };

    public override IEnumerable<IModInstaller> Installers => new IModInstaller[]
    {
        new StubbedGameInstaller()
    };
}
