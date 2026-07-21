using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using YogaDnsLocalizer.Core;

namespace YogaDnsLocalizer.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _exePath = "";
    private string _translationPath = "";
    private string _outputPath = "";
    private string _status = "请选择 YogaDNS.exe，然后导出翻译模板。";
    private string _searchText = "";
    private bool _showMissingOnly;
    private bool _busy;
    private TranslationCatalog _catalog = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<TranslationItem> Items { get; } = [];
    public string ExePath { get => _exePath; set { _exePath = value; OnChanged(); } }
    public string TranslationPath { get => _translationPath; set { _translationPath = value; OnChanged(); } }
    public string OutputPath { get => _outputPath; set { _outputPath = value; OnChanged(); } }
    public string Status { get => _status; private set { _status = value; OnChanged(); } }
    public string SearchText { get => _searchText; set { _searchText = value; OnChanged(); OnChanged(nameof(VisibleItems)); } }
    public bool ShowMissingOnly { get => _showMissingOnly; set { _showMissingOnly = value; OnChanged(); OnChanged(nameof(VisibleItems)); } }
    public bool Busy { get => _busy; private set { _busy = value; OnChanged(); } }
    public IEnumerable<TranslationItem> VisibleItems => Items.Where(item =>
        (!ShowMissingOnly || string.IsNullOrWhiteSpace(item.Translation)) &&
        (string.IsNullOrWhiteSpace(SearchText) || item.Original.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || item.Translation.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || item.Context.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

    public void SetStatus(string status) => Status = status;

    public void SetItems(IEnumerable<TranslationItem> items)
    {
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        OnChanged(nameof(VisibleItems));
    }

    public Task ChooseExeAsync()
    {
        var dialog = new OpenFileDialog { Filter = "YogaDNS 程序|YogaDNS.exe;*.exe|所有文件|*.*" };
        if (dialog.ShowDialog() == true) { ExePath = dialog.FileName; Status = "已选择：" + Path.GetFileName(ExePath); }
        return Task.CompletedTask;
    }

    public async Task ExportAsync()
    {
        if (string.IsNullOrWhiteSpace(ExePath)) throw new InvalidOperationException("请先选择 EXE。");
        await RunBusyAsync(async () =>
        {
            _catalog = await CreateService().ExtractCatalogAsync(ExePath, TempPath(), new Progress<LocalizationProgress>(p => Status = p.Message));
            SetItems(_catalog.Items);
            var dialog = new SaveFileDialog { Filter = "JSON 翻译文件|*.json", FileName = "YogaDNS-zh-CN.json" };
            if (dialog.ShowDialog() == true) { TranslationPath = dialog.FileName; File.WriteAllText(TranslationPath, _catalog.ToJson()); Status = $"已导出 {_catalog.Items.Count} 条翻译项。"; }
        });
    }

    public async Task ImportAsync()
    {
        var dialog = new OpenFileDialog { Filter = "JSON 翻译文件|*.json" };
        if (dialog.ShowDialog() != true) return;
        await LoadCatalogAsync(dialog.FileName);
    }

    public async Task LoadBuiltinAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "YogaDNS-zh-CN.json");
        if (!File.Exists(path)) throw new FileNotFoundException("未找到内置翻译。", path);
        await LoadCatalogAsync(path);
    }

    public Task SaveAsync()
    {
        var dialog = new SaveFileDialog { Filter = "JSON 翻译文件|*.json", FileName = string.IsNullOrWhiteSpace(TranslationPath) ? "YogaDNS-zh-CN.json" : Path.GetFileName(TranslationPath) };
        if (dialog.ShowDialog() != true) return Task.CompletedTask;
        SyncCatalog();
        File.WriteAllText(dialog.FileName, _catalog.ToJson());
        TranslationPath = dialog.FileName;
        Status = $"已保存 {_catalog.TranslatedCount} 条译文。";
        return Task.CompletedTask;
    }

    public async Task ApplyAsync()
    {
        if (string.IsNullOrWhiteSpace(ExePath)) throw new InvalidOperationException("请先选择 EXE。");
        if (Items.Count == 0) throw new InvalidOperationException("请先载入内置翻译或导入翻译文件。");
        var dialog = new SaveFileDialog { Filter = "汉化程序|*.exe", FileName = Path.GetFileNameWithoutExtension(ExePath) + ".zh-CN.exe" };
        if (dialog.ShowDialog() != true) return;
        OutputPath = dialog.FileName;
        await RunBusyAsync(async () =>
        {
            SyncCatalog();
            var result = await CreateService().ApplyAsync(ExePath, OutputPath, _catalog, TempPath(), new Progress<LocalizationProgress>(p => Status = p.Message));
            Status = $"完成：{result.OutputPath}（{result.TranslatedCount} 条译文）";
        });
    }

    private async Task LoadCatalogAsync(string path)
    {
        await RunBusyAsync(() =>
        {
            var imported = TranslationCatalog.FromJson(File.ReadAllText(path));
            if (Items.Count == 0)
            {
                _catalog = imported;
                SetItems(imported.Items);
            }
            else
            {
                var filled = 0;
                foreach (var item in Items.Where(x => string.IsNullOrWhiteSpace(x.Translation)))
                {
                    var match = imported.Items.FirstOrDefault(x => x.ResourceType == item.ResourceType && x.Original == item.Original && !string.IsNullOrWhiteSpace(x.Translation));
                    if (match is null) continue;
                    item.Translation = match.Translation;
                    item.Status = "已匹配";
                    filled++;
                }
                OnChanged(nameof(VisibleItems));
                Status = $"已从翻译文件匹配 {filled} 条，未匹配项可继续编辑。";
            }
            TranslationPath = path;
            return Task.CompletedTask;
        });
    }

    private void SyncCatalog()
    {
        _catalog.Items.Clear();
        _catalog.Items.AddRange(Items);
        if (string.IsNullOrWhiteSpace(_catalog.SourceFile)) _catalog.SourceFile = ExePath;
    }

    private ResourceHackerLocalizer CreateService()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tools", "ResourceHacker.exe");
        if (!File.Exists(path)) throw new FileNotFoundException("未找到内置 Resource Hacker。", path);
        return new ResourceHackerLocalizer(path);
    }
    private string TempPath() => Path.Combine(Path.GetTempPath(), "YogaDnsLocalizer", Guid.NewGuid().ToString("N"));
    private async Task RunBusyAsync(Func<Task> action) { Busy = true; try { await action(); } finally { Busy = false; } }
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
