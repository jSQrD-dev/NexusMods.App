using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusMods.Abstractions.Settings;
using NexusMods.App;
using NexusMods.App.BuildInfo;
using NexusMods.App.UI.Settings;
using NexusMods.DataModel;
using NexusMods.Games.RedEngine;
using NexusMods.Paths;
using NexusMods.StandardGameLocators.TestHelpers;
using NexusMods.UI.Tests.Framework;
using Xunit.DependencyInjection.Logging;

namespace NexusMods.UI.Tests;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var path = FileSystem.Shared.GetKnownPath(KnownPath.EntryDirectory).Combine("temp").Combine(Guid.NewGuid().ToString());
        path.CreateDirectory();

        services.AddUniversalGameLocator<Cyberpunk2077>(new Version("1.61"))
                .AddApp()
                .OverrideSettingsForTests<DataModelSettings>(settings => settings with
                {
                    UseInMemoryDataModel = true,
                })
                .OverrideSettingsForTests<LoadoutGridSettings>(settings => settings with
                {
                    ShowGameFiles = true,
                })
                .AddStubbedGameLocators()
                .AddSingleton<AvaloniaApp>()
                .AddLogging(builder => builder.AddXunitOutput().SetMinimumLevel(LogLevel.Debug))
                .Validate();
    }
}

