﻿using System.Collections.ObjectModel;
using NexusMods.Abstractions.DataModel.Entities.Mods;
using NexusMods.Abstractions.Games.DTO;
using NexusMods.Abstractions.Games.Loadouts;
using NexusMods.App.UI.Controls.DataGrid;
using NexusMods.App.UI.WorkspaceSystem;

namespace NexusMods.App.UI.RightContent.LoadoutGrid;

/// <summary>
/// View model for the loadout grid.
/// </summary>
public interface ILoadoutGridViewModel : IPageViewModelInterface, IRightContentViewModel
{
    public ReadOnlyObservableCollection<ModCursor> Mods { get; }
    public LoadoutId LoadoutId { get; set; }
    public string LoadoutName { get; }

    public ReadOnlyObservableCollection<IDataGridColumnFactory<LoadoutColumn>> Columns { get; }

    /// <summary>
    /// Add a mod to the loadout using the standard installer.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public Task AddMod(string path);

    /// <summary>
    /// Add a mod to the loadout using the advanced installer.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public Task AddModAdvanced(string path);

    /// <summary>
    /// Delete the mods from the loadout.
    /// </summary>
    /// <param name="modsToDelete"></param>
    /// <param name="commitMessage"></param>
    /// <returns></returns>
    public Task DeleteMods(IEnumerable<ModId> modsToDelete, string commitMessage);

}
