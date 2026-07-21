using System.IO;
using System.Windows;

namespace YogaDnsLocalizer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Loaded += (_, _) => FitToWorkArea();
        SizeChanged += (_, _) => FitToWorkArea();
        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;
    }

    private void FitToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        MaxHeight = Math.Max(MinHeight, workArea.Height - 16);
        MaxWidth = Math.Max(MinWidth, workArea.Width - 16);
        if (ActualHeight > MaxHeight) Height = MaxHeight;
        if (ActualWidth > MaxWidth) Width = MaxWidth;
        Left = Math.Max(workArea.Left, Math.Min(Left, workArea.Right - ActualWidth));
        Top = Math.Max(workArea.Top, Math.Min(Top, workArea.Bottom - ActualHeight));
    }

    private async void ChooseExe_Click(object sender, RoutedEventArgs e) => await RunAsync(_viewModel.ChooseExeAsync);
    private async void Export_Click(object sender, RoutedEventArgs e) => await RunAsync(_viewModel.ExportAsync);
    private async void Import_Click(object sender, RoutedEventArgs e) => await RunAsync(_viewModel.ImportAsync);
    private async void Builtin_Click(object sender, RoutedEventArgs e) => await RunAsync(_viewModel.LoadBuiltinAsync);
    private async void Save_Click(object sender, RoutedEventArgs e) => await RunAsync(_viewModel.SaveAsync);
    private async void Apply_Click(object sender, RoutedEventArgs e) => await RunAsync(_viewModel.ApplyAsync);

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { _viewModel.SetStatus("操作失败：" + ex.Message); }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 && Path.GetExtension(files[0]).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.ExePath = files[0];
            _viewModel.SetStatus("已拖入：" + Path.GetFileName(files[0]));
        }
    }
}
