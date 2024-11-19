using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Games.RedEngine.Cyberpunk2077;
using NexusMods.Games.RedEngine.Cyberpunk2077.SortOrder;
using NexusMods.Games.TestFramework;
using NexusMods.Paths;
using R3;
using ReactiveUI;
using StrawberryShake.Extensions;
using Xunit.Abstractions;

namespace NexusMods.Games.RedEngine.Tests;

public class RedModDeployToolTests : ACyberpunkIsolatedGameTest<Cyberpunk2077Game>
{
    private readonly RedModDeployTool _tool;

    public RedModDeployToolTests(ITestOutputHelper helper) : base(helper)
    {
        _tool = ServiceProvider.GetServices<ITool>().OfType<RedModDeployTool>().Single();
    }
    
    [Fact]
    public async Task LoadorderFileIsWrittenCorrectly()
    {
        var loadout = await SetupLoadout();
        await Verify(await WriteLoadoutFile(loadout));
    }
    
    [Theory]
    [InlineData("Driver_Shotguns", 3)]
    [InlineData("Driver_Shotguns", -3)]
    [InlineData("Driver_Shotguns", -11)]
    [InlineData("maestros_of_synth_body_heat_radio", -1)]
    [InlineData("maestros_of_synth_body_heat_radio", 10)]
    [InlineData("maestros_of_synth_the_dirge", -11)]
    public async Task MovingModsRelativelyResultsInCorrectOrdering(string name, int delta)
    {
        var loadout = await SetupLoadout();
        
        var factory = ServiceProvider.GetRequiredService<RedModSortableItemProviderFactory>();
        var provider = factory.GetLoadoutSortableItemProvider(loadout);
        
        var tsc1 = new TaskCompletionSource<Unit>();
        // avoid stalling the test on failure
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        cts.Token.Register(() => tsc1.TrySetCanceled(), useSynchronizationContext: false);
        
        // wait for the order to be updated
        provider.SortableItems
            .WhenAnyValue(coll => coll.Count)
            .Where(count => count == 12)
            .Distinct()
            .Subscribe(_ =>
            {
                if (!tsc1.Task.IsCompleted)
                {
                    tsc1.SetResult(Unit.Default);
                };
            } );
        await tsc1.Task;
        
        var order = provider.SortableItems;
        var specificGroup = order.OfType<RedModSortableItem>().Single(g => g.DisplayName == name);
        
        await provider.SetRelativePosition(specificGroup, delta);
        
        loadout = loadout.Rebase();

        await Verify(await WriteLoadoutFile(loadout)).UseParameters(name, delta);
    }


    private async Task<string> WriteLoadoutFile(Loadout.ReadOnly loadout)
    {
        await using var tempFile = TemporaryFileManager.CreateFile();
        loadout = loadout.Rebase();
        await _tool.WriteLoadOrderFile(tempFile.Path, loadout);
        return await tempFile.Path.ReadAllTextAsync();
    }

    private async Task<Loadout.ReadOnly> SetupLoadout()
    {
        var loadout = await CreateLoadout();
        var files = new[] { "one_mod.7z", "several_red_mods.7z" };
        
        await using var tempDir = TemporaryFileManager.CreateFolder();
        foreach (var file in files)
        {
            var sourcePath = FileSystem.GetKnownPath(KnownPath.EntryDirectory).Combine("LibraryArchiveInstallerTests/Resources/" + file);
            var copyPath = tempDir.Path.Combine(file);
            // Create copy to avoid "file in use" by other tests issues
            File.Copy(sourcePath.ToString(), copyPath.ToString(), overwrite: true);
            
            var libraryArchive = await RegisterLocalArchive(copyPath);
            await LibraryService.InstallItem(libraryArchive.AsLibraryFile().AsLibraryItem(), loadout);
        }
        
        loadout = loadout.Rebase();
        return loadout;
    }


}
