using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using DynamicData.Binding;
using Humanizer;
using NexusMods.Abstractions.Library.Models;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Abstractions.MnemonicDB.Attributes.Extensions;
using NexusMods.App.UI.Controls;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Paths;
using R3;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using CompositeDisposable = System.Reactive.Disposables.CompositeDisposable;

namespace NexusMods.App.UI.Pages.Library;

public readonly struct LibraryNodeId : IEquatable<LibraryNodeId>
{
    public readonly ulong Prefix;
    public readonly EntityId Id;

    public LibraryNodeId(ulong prefix, EntityId id)
    {
        Prefix = prefix;
        Id = id;
    }

    public static LibraryNodeId Empty => new();

    public static implicit operator LibraryNodeId(EntityId id) => new(0, id);
    public static implicit operator EntityId(LibraryNodeId id) => id.Id;

    public override string ToString() => $"{Prefix}:{Id}";

    public override int GetHashCode()
    {
        return HashCode.Combine(Prefix, Id.GetHashCode());
    }

    public bool Equals(LibraryNodeId other)
    {
        return Prefix == other.Prefix && Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        return obj is LibraryNodeId other && Equals(other);
    }

    public static bool operator ==(LibraryNodeId left, LibraryNodeId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(LibraryNodeId left, LibraryNodeId right)
    {
        return !(left == right);
    }
}

public class LibraryNode : Node<LibraryNode>
{
    public required LibraryNodeId Id { get; init; }
    [Reactive] public LibraryNodeId ParentId { get; set; }

    public ObservableCollection<LibraryLinkedLoadoutItem.ReadOnly> LinkedLoadoutItems { get; } = new();

    public required string Name { get; init; }

    protected const string DefaultVersion = "-";
    [Reactive] public string Version { get; set; } = DefaultVersion;

    protected static readonly Size DefaultSize = Size.Zero;
    [Reactive] public Size Size { get; set; } = DefaultSize;

    protected static readonly DateTime DefaultDateAddedToLoadout = DateTime.UnixEpoch;

    public required DateTime DateAddedToLibrary { get; init; }
    [Reactive] public string FormattedDateAddedToLibrary { get; private set; } = "-";

    [Reactive] public DateTime DateAddedToLoadout { get; set; }
    [Reactive] public string FormattedDateAddedToLoadout { get; private set; } = "-";

    public bool IsInLoadout => LinkedLoadoutItems.Any();

    public ReactiveCommand<System.Reactive.Unit, LibraryNode> AddToLoadoutCommand { get; }

    private readonly IDisposable _disposable;
    public LibraryNode()
    {
        AddToLoadoutCommand = ReactiveCommand.Create(() => this);

        var d = Disposable.CreateBuilder();

        LinkedLoadoutItems
            .ObserveCollectionChanges()
            .ToObservable()
            .Subscribe(this, static (_, node) =>
            {
                node.DateAddedToLoadout = node.LinkedLoadoutItems.Select(static item => item.GetCreatedAt()).DefaultIfEmpty(DateTime.UnixEpoch).Max();
            })
            .AddTo(ref d);

        Observable.Return(Unit.Default).Merge(Observable.IntervalFrame(periodFrame: 30, frameProvider: ObservableSystem.DefaultFrameProvider))
            .Subscribe(this, static (_, node) =>
            {
                var now = DateTime.Now;
                node.FormattedDateAddedToLibrary = Format(now, node.DateAddedToLibrary);
                node.FormattedDateAddedToLoadout = Format(now, node.DateAddedToLoadout);

                return;
                static string Format(DateTime now, DateTime other)
                {
                    if (other == DateTime.UnixEpoch || other == default(DateTime)) return "-";
                    return other.Humanize(dateToCompareAgainst: now);
                }
            })
            .AddTo(ref d);

        _disposable = d.Build();
    }

    public virtual LibraryItem.ReadOnly GetLibraryItemToInstall(IConnection connection)
    {
        return LibraryItem.Load(connection.Db, Id.Id);
    }

    private bool _isDisposed;
    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                _disposable.Dispose();
            }

            _isDisposed = true;
        }

        base.Dispose(disposing);
    }

    public static IColumn<LibraryNode> CreateNameColumn()
    {
        return new TextColumn<LibraryNode, string>(
            header: "Name",
            getter: model => model.Name,
            options: new TextColumnOptions<LibraryNode>
            {
                CompareAscending = static (a, b) => string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase),
                CompareDescending = static (a, b) => string.Compare(b?.Name, a?.Name, StringComparison.OrdinalIgnoreCase),
                IsTextSearchEnabled = true,
                CanUserResizeColumn = true,
                CanUserSortColumn = true,
            }
        )
        {
            SortDirection = ListSortDirection.Ascending,
            Tag = "name",
        };
    }

    public static IColumn<LibraryNode> CreateVersionColumn()
    {
        return new TextColumn<LibraryNode, string>(
            header: "Version",
            getter: model => model.Version,
            options: new TextColumnOptions<LibraryNode>
            {
                CompareAscending = static (a, b) => string.Compare(a?.Version, b?.Version, StringComparison.OrdinalIgnoreCase),
                CompareDescending = static (a, b) => string.Compare(b?.Version, a?.Version, StringComparison.OrdinalIgnoreCase),
                IsTextSearchEnabled = true,
                CanUserResizeColumn = true,
                CanUserSortColumn = true,
            }
        )
        {
            Tag = "version",
        };
    }

    public static IColumn<LibraryNode> CreateSizeColumn()
    {
        return new TextColumn<LibraryNode, Size>(
            header: "Size",
            getter: model => model.Size,
            options: new TextColumnOptions<LibraryNode>
            {
                CompareAscending = static (a, b) => a is null ? -1 : a.Size.CompareTo(b?.Size ?? Size.Zero),
                CompareDescending = static (a, b) => b is null ? -1 : b.Size.CompareTo(a?.Size ?? Size.Zero),
                IsTextSearchEnabled = false,
                CanUserResizeColumn = true,
                CanUserSortColumn = true,
            }
        )
        {
            Tag = "size",
        };
    }

    public static IColumn<LibraryNode> CreateDateAddedToLibraryColumn()
    {
        return new TextColumn<LibraryNode, string>(
            header: "Date added to Library",
            getter: model => model.FormattedDateAddedToLibrary,
            options: new TextColumnOptions<LibraryNode>
            {
                CompareAscending = static (a, b) => DateTime.Compare(a?.DateAddedToLibrary ?? DateTime.UnixEpoch, b?.DateAddedToLibrary ?? DateTime.UnixEpoch),
                CompareDescending = static (a, b) => DateTime.Compare(b?.DateAddedToLibrary ?? DateTime.UnixEpoch, a?.DateAddedToLibrary ?? DateTime.UnixEpoch),
                IsTextSearchEnabled = false,
                CanUserResizeColumn = true,
                CanUserSortColumn = true,
            }
        )
        {
            Tag = "date_added_to_library",
        };
    }

    public static IColumn<LibraryNode> CreateDateAddedToLoadoutColumn()
    {
        return new TextColumn<LibraryNode, string>(
            header: "Date added to Loadout",
            getter: model => model.FormattedDateAddedToLoadout,
            options: new TextColumnOptions<LibraryNode>
            {
                CompareAscending = static (a, b) => DateTime.Compare(a?.DateAddedToLoadout ?? DateTime.UnixEpoch, b?.DateAddedToLoadout ?? DateTime.UnixEpoch),
                CompareDescending = static (a, b) => DateTime.Compare(b?.DateAddedToLoadout ?? DateTime.UnixEpoch, a?.DateAddedToLoadout ?? DateTime.UnixEpoch),
                IsTextSearchEnabled = false,
                CanUserResizeColumn = true,
                CanUserSortColumn = true,
            }
        )
        {
            Tag = "date_added_to_loadout",
        };
    }

    public static IColumn<LibraryNode> CreateAddToLoadoutButtonColumn()
    {
        return new TemplateColumn<LibraryNode>(
            header: "Install",
            cellTemplateResourceKey: "AddToLoadoutButtonColumnTemplate",
            options: new TemplateColumnOptions<LibraryNode>
            {
                CompareAscending = static (a, b) => a?.IsInLoadout.CompareTo(b?.IsInLoadout) ?? 1,
                CompareDescending = static (a, b) => b?.IsInLoadout.CompareTo(a?.IsInLoadout) ?? 1,
                IsTextSearchEnabled = false,
                CanUserResizeColumn = true,
                CanUserSortColumn = true,
            }
        )
        {
            Tag = "add_to_loadout_button",
        };
    }
}
