using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI;

namespace NexusMods.App.UI.WorkspaceSystem;

public class NewTabPageViewModel : APageViewModel<INewTabPageViewModel>, INewTabPageViewModel
{
    private readonly SourceList<INewTabPageSectionItemViewModel> _itemSource = new();

    private readonly ReadOnlyObservableCollection<INewTabPageSectionViewModel> _sections;
    public ReadOnlyObservableCollection<INewTabPageSectionViewModel> Sections => _sections;

    public NewTabPageViewModel(IWorkspaceController workspaceController, PageDiscoveryDetails[] discoveryDetails) : base(workspaceController)
    {
        _itemSource.Edit(list =>
        {
            var toAdd = discoveryDetails
                .Select(details => (INewTabPageSectionItemViewModel)new NewTabPageSectionItemViewModel(details));

            list.AddRange(toAdd);
        });

        _itemSource
            .Connect()
            .GroupOn(item => item.SectionName)
            .Transform(x => (INewTabPageSectionViewModel)new NewTabPageSectionViewModel(x.GroupKey, x.List))
            .Bind(out _sections)
            .Subscribe();

        this.WhenActivated(disposables =>
        {
            _itemSource
                .Connect()
                .MergeMany(item => item.SelectItemCommand)
                .SubscribeWithErrorLogging(pageData =>
                {
                    WorkspaceController.OpenPage(WorkspaceId, pageData, new OpenPageBehavior(new OpenPageBehavior.ReplaceTab(PanelId, TabId)));
                })
                .DisposeWith(disposables);
        });
    }
}
