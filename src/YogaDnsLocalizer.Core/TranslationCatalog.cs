using System.Text.Json;
using System.Text.Json.Serialization;

namespace YogaDnsLocalizer.Core;

public enum ResourceType
{
    Menu,
    Dialog,
    String
}

public sealed record ResourceEntry(
    ResourceType ResourceType,
    int ResourceId,
    string Original,
    string SourceText,
    int ControlId = 0,
    string Context = "");

public sealed class TranslationItem
{
    public ResourceType ResourceType { get; set; }
    public int ResourceId { get; set; }
    public int ControlId { get; set; }
    public string Original { get; set; } = "";
    public string Translation { get; set; } = "";
    public string Context { get; set; } = "";
    public string Status { get; set; } = "待翻译";
}

public sealed class TranslationCatalog
{
    public string FormatVersion { get; set; } = "1";
    public string Name { get; set; } = "YogaDNS 简体中文";
    public string SourceFile { get; set; } = "";
    public string SourceSha256 { get; set; } = "";
    public List<TranslationItem> Items { get; set; } = [];

    [JsonIgnore]
    public int TranslatedCount => Items.Count(x => !string.IsNullOrWhiteSpace(x.Translation));

    public TranslationItem? Find(ResourceEntry entry)
    {
        return Items.FirstOrDefault(item =>
            item.ResourceType == entry.ResourceType &&
            !string.IsNullOrWhiteSpace(item.Translation) &&
            ((item.ResourceId == entry.ResourceId && item.ControlId == entry.ControlId && item.Original == entry.Original)
             || item.Original == entry.Original));
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static TranslationCatalog FromJson(string json)
    {
        var value = JsonSerializer.Deserialize<TranslationCatalog>(json, JsonOptions);
        return value ?? throw new InvalidDataException("翻译文件为空或格式无效。");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
