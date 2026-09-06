namespace ServiceLib.ViewModels;

public class SubSettingViewModel : MyReactiveObject
{
    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();
    public Interaction<string, Unit> ShareSubInteraction { get; } = new();
    public Interaction<Unit, string?> BrowseSubscriptionFileInteraction { get; } = new();

    public IObservableCollection<SubItem> SubItems { get; } = new ObservableCollectionExtended<SubItem>();

    [Reactive]
    public SubItem SelectedSource { get; set; }

    public IList<SubItem> SelectedSources { get; set; }

    public ReactiveCommand<Unit, Unit> SubAddCmd { get; }
    public ReactiveCommand<Unit, Unit> SubImportFromFileCmd { get; }
    public ReactiveCommand<Unit, Unit> SubDeleteCmd { get; }
    public ReactiveCommand<Unit, Unit> SubEditCmd { get; }
    public ReactiveCommand<Unit, Unit> SubShareCmd { get; }
    public bool IsModified { get; set; }

    public SubSettingViewModel()
    {
        _config = AppManager.Instance.Config;

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedSource,
           selectedSource => selectedSource != null && !selectedSource.Id.IsNullOrEmpty());

        SubAddCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(true);
        });
        SubImportFromFileCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var fileName = await BrowseSubscriptionFileInteraction.Handle(Unit.Default);
            await ImportFromFileAsync(fileName);
        });
        SubDeleteCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DeleteSubAsync();
        }, canEditRemove);
        SubEditCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(false);
        }, canEditRemove);
        SubShareCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ShareSubInteraction.Handle(SelectedSource?.Url);
        }, canEditRemove);

        _ = Init();
    }

    private async Task Init()
    {
        SelectedSource = new();

        await RefreshSubItems();
    }

    public async Task RefreshSubItems()
    {
        SubItems.Clear();
        SubItems.AddRange(await AppManager.Instance.SubItems());
    }

    public async Task EditSubAsync(bool blNew)
    {
        SubItem item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = await AppManager.Instance.GetSubItem(SelectedSource?.Id);
            if (item is null)
            {
                return;
            }
        }
        var subEditViewModel = new SubEditViewModel(item);
        if (await AppManager.Instance.WindowDialog.ShowDialogAsync(subEditViewModel) == true)
        {
            await RefreshSubItems();
            IsModified = true;
        }
    }

    public async Task ImportFromFileAsync(string? fileName)
    {
        if (fileName.IsNullOrEmpty() || !File.Exists(fileName)) return;
        var entries = SubscriptionFileImportService.Parse(File.ReadLines(fileName, Encoding.UTF8), SubItems.Select(x => x.Remarks));
        if (entries.Count == 0) { NoticeManager.Instance.Enqueue(ResUI.OperationFailed); return; }
        var imported = 0;
        foreach (var entry in entries)
        {
            var item = new SubItem { Id = string.Empty, Remarks = entry.Remarks, Url = entry.Url };
            if (await ConfigHandler.AddSubItem(_config, item) == 0) imported++;
        }
        await RefreshSubItems();
        if (imported > 0) { IsModified = true; NoticeManager.Instance.Enqueue($"Imported subscriptions: {imported}"); }
        else { NoticeManager.Instance.Enqueue(ResUI.OperationFailed); }
    }

    private async Task DeleteSubAsync()
    {
        if (await ShowYesNoInteraction.Handle(ResUI.RemoveServer) == false)
        {
            return;
        }

        foreach (var it in SelectedSources ?? [SelectedSource])
        {
            await ConfigHandler.DeleteSubItem(_config, it.Id);
        }
        await RefreshSubItems();
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        IsModified = true;
    }
}
