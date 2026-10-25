using ReflexRenamer.Core.Rename;

namespace ReflexRenamer.Core.Tests;

public class FolderDisplayTests
{
    [Theory]
    [InlineData(@"C:\a\b", @"C:\a\b", ".")]
    [InlineData(@"C:\a\b\", @"C:\a\b", ".")]
    [InlineData(@"C:\a\b", @"C:\a\b\sub", "sub")]
    [InlineData(@"C:\a\b", @"C:\a\b\sub\deep", @"sub\deep")]
    [InlineData(@"C:\a\b", @"C:\A\B\Sub", "Sub")]
    [InlineData(@"C:\a\b", @"C:\a", @"C:\a")]
    [InlineData(@"C:\a\b", @"C:\a\bc", @"C:\a\bc")]
    [InlineData(@"C:\a\b", @"D:\x", @"D:\x")]
    [InlineData(@"C:\", @"C:\x", "x")]
    [InlineData("", @"C:\x", @"C:\x")]
    public void Relative(string baseFolder, string folder, string expected) =>
        Assert.Equal(expected, FolderDisplay.Relative(baseFolder, folder));
}
