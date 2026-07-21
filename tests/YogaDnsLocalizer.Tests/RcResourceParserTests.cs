using YogaDnsLocalizer.Core;

namespace YogaDnsLocalizer.Tests;

public sealed class RcResourceParserTests
{
    [Fact]
    public void Extracts_menu_caption_and_preserves_mnemonic()
    {
        const string rc = "MENUITEM \"DNS &Servers...\", 32794";

        var entries = RcResourceParser.Parse(ResourceType.Menu, 128, rc);

        var entry = Assert.Single(entries);
        Assert.Equal("DNS &Servers...", entry.Original);
        Assert.Equal(32794, entry.ControlId);
    }

    [Fact]
    public void Applies_translation_without_touching_unmapped_text()
    {
        const string rc = "CAPTION \"About YogaDNS\"\r\nCONTROL \"OK\", 1";
        var entries = new[]
        {
            new ResourceEntry(ResourceType.Dialog, 131, "About YogaDNS", "About YogaDNS", 0),
            new ResourceEntry(ResourceType.Dialog, 131, "OK", "OK", 1)
        };
        var catalog = new TranslationCatalog();
        catalog.Items.Add(new TranslationItem
        {
            ResourceType = ResourceType.Dialog,
            ResourceId = 131,
            Original = "About YogaDNS",
            Translation = "关于 YogaDNS"
        });

        var output = RcResourceParser.ApplyTranslations(rc, entries, catalog);

        Assert.Contains("CAPTION \"关于 YogaDNS\"", output);
        Assert.Contains("CONTROL \"OK\", 1", output);
    }
}
