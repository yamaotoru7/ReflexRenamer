using ReflexRenamer.Core.Resources;

namespace ReflexRenamer.Core.Tests;

public class ResourceSetTests
{
    private static ResourceSet Load()
    {
        using var json = File.OpenRead(Path.Combine(ReferenceFiles.RepoRoot, "docs", "spec", "resources.json"));
        return ResourceSet.Load(json);
    }

    private static IEnumerable<MenuTemplate> All(IEnumerable<MenuTemplate> items) =>
        items.SelectMany(i => i.Children is null ? [i] : All(i.Children).Prepend(i));

    [Fact]
    public void WithoutCommandsRemovesItemsEverywhere()
    {
        var original = Load();
        Assert.Contains(original.Menus.Values.SelectMany(All), i => i.Id == 32887);

        var set = original.WithoutCommands(32914, 32887);

        Assert.DoesNotContain(set.Menus.Values.SelectMany(All), i => i.Id is 32914 or 32887);
        Assert.Equal(original.Dialogs.Count, set.Dialogs.Count);
    }

    [Fact]
    public void WithoutCommandsTidiesSeparators()
    {
        var set = Load().WithoutCommands(32914, 32887);

        foreach (var list in set.Menus.Values.SelectMany(All).Where(i => i.Children is not null).Select(i => i.Children!))
        {
            if (list.Count == 0)
            {
                continue;
            }
            Assert.False(list[0].Separator);
            Assert.False(list[^1].Separator);
            Assert.DoesNotContain(list.Zip(list.Skip(1)), p => p.First.Separator && p.Second.Separator);
        }
    }
}
