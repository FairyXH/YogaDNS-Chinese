using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace YogaDnsLocalizer.Core;

public sealed record LocalizationProgress(string Message, int Percent);
public sealed record LocalizationResult(string OutputPath, string LogPath, int TranslatedCount);

public sealed class ResourceHackerLocalizer
{
    private readonly string _resourceHackerPath;

    public ResourceHackerLocalizer(string resourceHackerPath) => _resourceHackerPath = resourceHackerPath;

    public async Task<TranslationCatalog> ExtractCatalogAsync(string exePath, string workDirectory, IProgress<LocalizationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateInput(exePath);
        Directory.CreateDirectory(workDirectory);
        var menuPath = Path.Combine(workDirectory, "menus.rc");
        var dialogPath = Path.Combine(workDirectory, "dialogs.rc");
        var stringPath = Path.Combine(workDirectory, "strings.rc");
        var logPath = Path.Combine(workDirectory, "export.log");
        await ExtractResourcesAsync(exePath, menuPath, dialogPath, stringPath, logPath, cancellationToken);
        progress?.Report(new("正在分析资源", 55));

        var catalog = new TranslationCatalog { SourceFile = exePath, SourceSha256 = await Sha256Async(exePath, cancellationToken) };
        AddEntries(catalog, ResourceType.Menu, menuPath);
        AddEntries(catalog, ResourceType.Dialog, dialogPath);
        AddEntries(catalog, ResourceType.String, stringPath);
        progress?.Report(new($"已识别 {catalog.Items.Count} 条界面文本", 100));
        return catalog;
    }

    public async Task<LocalizationResult> ApplyAsync(string exePath, string outputPath, TranslationCatalog catalog, string workDirectory, IProgress<LocalizationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateInput(exePath);
        if (File.Exists(outputPath)) throw new IOException("输出文件已存在，请选择新的文件名。");
        Directory.CreateDirectory(workDirectory);
        var menuPath = Path.Combine(workDirectory, "menus.rc");
        var dialogPath = Path.Combine(workDirectory, "dialogs.rc");
        var stringPath = Path.Combine(workDirectory, "strings.rc");
        var menuRes = Path.Combine(workDirectory, "menus.zh-CN.res");
        var dialogRes = Path.Combine(workDirectory, "dialogs.zh-CN.res");
        var stringRes = Path.Combine(workDirectory, "strings.zh-CN.res");
        var logPath = Path.Combine(workDirectory, "apply.log");
        await ExtractResourcesAsync(exePath, menuPath, dialogPath, stringPath, logPath, cancellationToken);
        progress?.Report(new("正在应用翻译", 40));
        await TranslateAndCompileAsync(menuPath, menuRes, ResourceType.Menu, catalog, cancellationToken);
        await TranslateAndCompileAsync(dialogPath, dialogRes, ResourceType.Dialog, catalog, cancellationToken);
        await TranslateAndCompileAsync(stringPath, stringRes, ResourceType.String, catalog, cancellationToken);

        var script = Path.Combine(workDirectory, "write-resources.txt");
        File.WriteAllText(script, $"[FILENAMES]\r\nExe={exePath}\r\nSaveAs={outputPath}\r\nLog={logPath}\r\n\r\n[COMMANDS]\r\n-add {menuRes}, MENU,,2052\r\n-add {dialogRes}, DIALOG,,2052\r\n-add {stringRes}, STRINGTABLE,,2052\r\n", Encoding.Unicode);
        await RunAsync(["-script", script], cancellationToken);
        if (!File.Exists(outputPath)) throw new IOException($"资源写入完成，但未生成输出文件。日志：{logPath}");
        progress?.Report(new("汉化文件已生成", 100));
        return new LocalizationResult(outputPath, logPath, catalog.TranslatedCount);
    }

    private async Task ExtractResourcesAsync(string exePath, string menuPath, string dialogPath, string stringPath, string logPath, CancellationToken token)
    {
        var script = Path.Combine(Path.GetDirectoryName(menuPath)!, "extract-resources.txt");
        File.WriteAllText(script, $"[FILENAMES]\r\nExe={exePath}\r\nLog={logPath}\r\n\r\n[COMMANDS]\r\n-extract {menuPath}, MENU,,1033\r\n-extract {dialogPath}, DIALOG,,1033\r\n-extract {stringPath}, STRINGTABLE,,1033\r\n", Encoding.Unicode);
        await RunAsync(["-script", script], token);
        foreach (var path in new[] { menuPath, dialogPath, stringPath })
            if (!File.Exists(path)) throw new IOException($"未能提取资源：{Path.GetFileName(path)}");
    }

    private async Task TranslateAndCompileAsync(string rcPath, string resPath, ResourceType type, TranslationCatalog catalog, CancellationToken token)
    {
        var text = File.ReadAllText(rcPath, Encoding.Unicode);
        var entries = RcResourceParser.Parse(type, 0, text);
        var translated = RcResourceParser.ApplyTranslations(text, entries, catalog)
            .Replace("LANGUAGE LANG_ENGLISH, SUBLANG_ENGLISH_US", "LANGUAGE LANG_CHINESE, SUBLANG_CHINESE_SIMPLIFIED");
        if (type == ResourceType.Dialog)
            translated = translated.Replace("FONT 8, \"MS Shell Dlg\"", "FONT 9, \"Microsoft YaHei UI\"")
                .Replace("FONT 8, \"MS Shell Dlg 2\"", "FONT 9, \"Microsoft YaHei UI\"");
        File.WriteAllText(rcPath, translated, Encoding.Unicode);
        await RunAsync(["-open", rcPath, "-save", resPath, "-action", "compile"], token);
        if (!File.Exists(resPath)) throw new IOException($"资源编译失败：{Path.GetFileName(rcPath)}");
    }

    private async Task RunAsync(string[] args, CancellationToken token)
    {
        var psi = new ProcessStartInfo(_resourceHackerPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 Resource Hacker。");
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        var output = await stdout + await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Resource Hacker 执行失败（{process.ExitCode}）。\r\n{output}");
    }

    private static void AddEntries(TranslationCatalog catalog, ResourceType type, string path)
    {
        var text = File.ReadAllText(path, Encoding.Unicode);
        foreach (var entry in RcResourceParser.Parse(type, 0, text))
            if (!catalog.Items.Any(x => x.ResourceType == type && x.Original == entry.Original))
                catalog.Items.Add(new TranslationItem { ResourceType = type, ResourceId = entry.ResourceId, ControlId = entry.ControlId, Original = entry.Original, Context = entry.Context });
    }

    private static void ValidateInput(string path)
    {
        if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) throw new FileNotFoundException("请选择有效的 EXE 文件。", path);
    }

    private static async Task<string> Sha256Async(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }
}
