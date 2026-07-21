using YogaDnsLocalizer.Core;

namespace YogaDnsLocalizer.Tests;

public sealed class TranslationCatalogTests
{
    [Fact]
    public void Matches_by_resource_and_original_text_when_id_changes()
    {
        var catalog = new TranslationCatalog();
        catalog.Items.Add(new TranslationItem
        {
            ResourceType = ResourceType.String,
            ResourceId = 32800,
            Original = "About YogaDNS",
            Translation = "关于 YogaDNS"
        });

        var match = catalog.Find(new ResourceEntry(ResourceType.String, 99999, "About YogaDNS", "About YogaDNS"));

        Assert.NotNull(match);
        Assert.Equal("关于 YogaDNS", match!.Translation);
    }

    [Fact]
    public void Rejects_placeholder_loss()
    {
        var result = PlaceholderValidator.Validate("Save changes to %1?", "保存更改？");

        Assert.False(result.IsValid);
        Assert.Contains("%1", result.Message);
    }

    [Fact]
    public void Json_round_trip_preserves_translation_items()
    {
        var catalog = new TranslationCatalog
        {
            Name = "YogaDNS 简体中文"
        };
        catalog.Items.Add(new TranslationItem
        {
            ResourceType = ResourceType.Menu,
            ResourceId = 128,
            ControlId = 0,
            Original = "DNS Servers",
            Translation = "DNS 服务器",
            Context = "主菜单"
        });

        var json = catalog.ToJson();
        var restored = TranslationCatalog.FromJson(json);

        Assert.Equal(catalog.Name, restored.Name);
        Assert.Single(restored.Items);
        Assert.Equal("DNS 服务器", restored.Items[0].Translation);
    }
}
