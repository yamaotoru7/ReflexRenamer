using ReflexRenamer.Core.Settings;

namespace ReflexRenamer.Core.Tests;

public sealed class BarStateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ReflexRenamerTests", Guid.NewGuid().ToString("N"));

    public BarStateTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private AppSettings NewSettings() => AppSettings.Load(Path.Combine(_dir, AppSettings.FileName));

    [Fact]
    public void ReadsWidthsAndOrderFromOriginalIni()
    {
        // 配布物の ini：フォルダ一覧を 396px に広げてある（XPos -2〜394、入力フォーム 392〜663）
        var state = BarState.Load(AppSettings.Load(ReferenceFiles.Get("Flexible Renamer.ini")));
        Assert.NotNull(state);
        Assert.Equal([(126, 396), (127, 271)], state.Panes.Select(p => (p.Id, p.Width)));
        Assert.All(state.Panes, p => Assert.True(p.InDock));
        Assert.Equal(DockedPane.Unspecified, state.Panes[0].FloatX);
        Assert.Equal((175, 175), state.FloatSizeOf(126));
        Assert.Equal(-2, state.Lefts()[126]);
        Assert.Equal(392, state.Lefts()[127]);
        Assert.Equal(661, state.ContentLeft());
    }

    [Fact]
    public void ReturnsNullWithoutBarState()
    {
        Assert.Null(BarState.Load(NewSettings()));
    }

    [Fact]
    public void DefaultMatchesFixedLayoutOfOriginal()
    {
        // 起動直後のオリジナル版：126 は -2 から 175px、127 は 171 から、ファイル一覧は 440 から
        var state = BarState.Default;
        Assert.Equal(-2, state.Lefts()[126]);
        Assert.Equal(171, state.Lefts()[127]);
        Assert.Equal(440, state.ContentLeft());
    }

    [Fact]
    public void HiddenPaneIsSkippedInLayout()
    {
        var state = BarState.Default;
        var lefts = state.Lefts(id => id != 126);
        Assert.False(lefts.ContainsKey(126));
        Assert.Equal(-2, lefts[127]);
        Assert.Equal(267, state.ContentLeft(id => id != 126));
    }

    [Fact]
    public void WritesSameFormatAsOriginal()
    {
        var s = NewSettings();
        BarState.Default.Save(s, dockHeight: 610, screenWidth: 3840, screenHeight: 1200);

        // オリジナル版を ini なしで起動して閉じたときの値（.work/sandbox/original の実測）
        Assert.Equal(7, s.GetInt("BarState-Summary", "Bars", 0));
        Assert.Equal("3840", s.Get("BarState-Summary", "ScreenCX"));
        Assert.Equal("1200", s.Get("BarState-Summary", "ScreenCY"));
        Assert.Equal("59392", s.Get("BarState-Bar0", "BarID"));
        Assert.Equal("59393", s.Get("BarState-Bar3", "BarID"));
        Assert.Equal("59420", s.Get("BarState-Bar4", "BarID"));
        Assert.Equal("5", s.Get("BarState-Bar4", "Bars"));
        Assert.Equal(["0", "126", "0", "127", "0"], Enumerable.Range(0, 5).Select(i => s.Get("BarState-Bar4", $"Bar#{i}")));

        Assert.Equal("127", s.Get("BarState-Bar5", "BarID"));
        Assert.Equal("171", s.Get("BarState-Bar5", "XPos"));
        Assert.Equal("-2", s.Get("BarState-Bar5", "YPos"));
        Assert.Equal("1", s.Get("BarState-Bar5", "Docking"));
        Assert.Equal("171", s.Get("BarState-Bar5", "MRUDockLeftPos"));
        Assert.Equal("442", s.Get("BarState-Bar5", "MRUDockRightPos"));
        Assert.Equal("612", s.Get("BarState-Bar5", "MRUDockBottomPos"));
        Assert.Equal("8196", s.Get("BarState-Bar5", "MRUFloatStyle"));
        Assert.Equal("-2147483648", s.Get("BarState-Bar5", "MRUFloatXPos"));
        Assert.Equal("126", s.Get("BarState-Bar6", "BarID"));
        Assert.Equal("-2", s.Get("BarState-Bar6", "XPos"));

        Assert.Equal("175", s.Get("BarState-SCBar-126", "sizeVertCX"));
        Assert.Equal("614", s.Get("BarState-SCBar-126", "sizeVertCY"));
        Assert.Equal("175", s.Get("BarState-SCBar-126", "sizeHorzCY"));
        Assert.Equal("271", s.Get("BarState-SCBar-127", "sizeVertCX"));
        Assert.Equal("503", s.Get("BarState-SCBar-127", "sizeHorzCY"));
    }

    [Fact]
    public void RoundTripsChangedWidthAndOrder()
    {
        string path = Path.Combine(_dir, AppSettings.FileName);
        var s = AppSettings.Load(path);
        var state = BarState.Default.WithWidth(126, 300).MoveTo(127, 0);
        Assert.Equal([127, 126], state.Panes.Select(p => p.Id));
        state.Save(s, 500, 1920, 1080);
        s.Save();

        var again = BarState.Load(AppSettings.Load(path));
        Assert.NotNull(again);
        Assert.Equal([(127, 271), (126, 300)], again.Panes.Select(p => (p.Id, p.Width)));
        Assert.Equal(["0", "127", "0", "126", "0"], Enumerable.Range(0, 5).Select(i => s.Get("BarState-Bar4", $"Bar#{i}")));
        Assert.Equal("267", s.Get("BarState-Bar6", "XPos"));
    }

    [Fact]
    public void KeepsHorzAndFloatSizes()
    {
        // 配布物の ini では 126 の sizeHorzCX/sizeFloatCX は 175 のまま、sizeVertCX だけ 396
        var s = AppSettings.Load(ReferenceFiles.Get("Flexible Renamer.ini"));
        var state = BarState.Load(s)!.WithWidth(126, 250);
        state.Save(s, 1009, 3840, 1200);
        Assert.Equal("175", s.Get("BarState-SCBar-126", "sizeHorzCX"));
        Assert.Equal("175", s.Get("BarState-SCBar-126", "sizeFloatCY"));
        Assert.Equal("250", s.Get("BarState-SCBar-126", "sizeVertCX"));
        Assert.Equal("1013", s.Get("BarState-SCBar-126", "sizeVertCY"));
    }

    [Fact]
    public void FillsMissingPanesAndIgnoresOtherBars()
    {
        var s = NewSettings();
        s.Set("BarState-Summary", "Bars", 2);
        s.Set("BarState-Bar0", "BarID", 59392);
        s.Set("BarState-Bar1", "BarID", BarState.LeftDockBarId);
        s.Set("BarState-Bar1", "Bars", 3);
        s.Set("BarState-Bar1", "Bar#0", 0);
        s.Set("BarState-Bar1", "Bar#1", 127);
        s.Set("BarState-Bar1", "Bar#2", 0);
        s.Set("BarState-SCBar-127", "sizeVertCX", 10);

        var state = BarState.Load(s)!;
        // 126 は並びになかった（浮かせていたなど）ので既定の位置（左端）に、幅は既定値。小さすぎる幅は最小値にする
        Assert.Equal([(126, 175), (127, BarState.MinWidth)], state.Panes.Select(p => (p.Id, p.Width)));
    }

    [Theory]
    [InlineData(126, 300, 300)]
    [InlineData(126, 10, 33)]
    [InlineData(126, 900, 1000 - 100 - 265)]
    [InlineData(127, 271 + 500, 1000 - 100 - 169)]
    public void ClampsWidthToKeepFileList(int id, int requested, int expected)
    {
        // ドック領域 1000px、ファイル一覧に 100px 残す
        Assert.Equal(expected, BarState.Default.ClampWidth(id, requested, 1000, BarState.MinWidth, 100));
        var widened = BarState.Default.WithWidth(id, expected);
        Assert.True(1000 - widened.ContentLeft() >= 100);
    }

    [Fact]
    public void WritesFloatingPaneAsMiniFrameDockBar()
    {
        var s = NewSettings();
        BarState.Default.Float(127, 500, 300, 280, 400).Save(s, 610, 3840, 1200);

        // 左のドックバーには 126 だけ
        Assert.Equal("3", s.Get("BarState-Bar4", "Bars"));
        Assert.Equal(["0", "126", "0"], Enumerable.Range(0, 3).Select(i => s.Get("BarState-Bar4", $"Bar#{i}")));
        Assert.Null(s.Get("BarState-Bar4", "Bar#3"));
        // 浮かせたウィンドウの中のドックバー（MFC の CMiniDockFrameWnd）。XPos/YPos がウィンドウの位置
        Assert.Equal("59423", s.Get("BarState-Bar5", "BarID"));
        Assert.Equal("1", s.Get("BarState-Bar5", "Floating"));
        Assert.Equal("500", s.Get("BarState-Bar5", "XPos"));
        Assert.Equal("300", s.Get("BarState-Bar5", "YPos"));
        Assert.Equal(["0", "127", "0"], Enumerable.Range(0, 3).Select(i => s.Get("BarState-Bar5", $"Bar#{i}")));
        // ペイン：MRUFloat* に浮かせた位置、MRUDock* にドッキングに戻す位置
        Assert.Equal("127", s.Get("BarState-Bar6", "BarID"));
        Assert.Equal("1", s.Get("BarState-Bar6", "Docking"));
        Assert.Equal("59420", s.Get("BarState-Bar6", "MRUDockID"));
        Assert.Equal("171", s.Get("BarState-Bar6", "MRUDockLeftPos"));
        Assert.Equal("8196", s.Get("BarState-Bar6", "MRUFloatStyle"));
        Assert.Equal("500", s.Get("BarState-Bar6", "MRUFloatXPos"));
        Assert.Equal("300", s.Get("BarState-Bar6", "MRUFloatYPos"));
        Assert.Equal("126", s.Get("BarState-Bar7", "BarID"));
        Assert.Equal("8", s.Get("BarState-Summary", "Bars"));
        Assert.Equal("280", s.Get("BarState-SCBar-127", "sizeFloatCX"));
        Assert.Equal("400", s.Get("BarState-SCBar-127", "sizeFloatCY"));
    }

    [Fact]
    public void RoundTripsFloatingAndHiddenPanes()
    {
        string path = Path.Combine(_dir, AppSettings.FileName);
        var s = AppSettings.Load(path);
        BarState.Default.Float(126, -1500, 40, 200, 600).SetHidden(127, true).Save(s, 610, 3840, 1200);
        s.Save();

        var again = BarState.Load(AppSettings.Load(path))!;
        var tree = again.Find(126)!;
        Assert.True(tree.Floating);
        Assert.False(tree.Hidden);
        Assert.Equal((-1500, 40), (tree.FloatX, tree.FloatY));
        Assert.Equal((200, 600), again.FloatSizeOf(126));
        // 浮かせたペインも元の並び（左端）を覚えている
        Assert.Equal([126, 127], again.Panes.Select(p => p.Id));
        var form = again.Find(127)!;
        Assert.True(form.Hidden);
        Assert.False(form.Floating);
        Assert.Equal("127", AppSettings.Load(path).Get("BarState-Bar6", "BarID"));
        Assert.Equal("0", AppSettings.Load(path).Get("BarState-Bar6", "Visible"));
        // 列に出ているペインはない
        Assert.Empty(again.Lefts());
        Assert.Equal(0, again.ContentLeft());

        // ドッキングに戻すと元の位置と幅、浮かせた位置は MRUFloat* に残る
        var docked = again.Dock(126).SetHidden(127, false);
        Assert.Equal(-2, docked.Lefts()[126]);
        Assert.Equal(171, docked.Lefts()[127]);
        docked.Save(s, 610, 3840, 1200);
        Assert.Equal("7", s.Get("BarState-Summary", "Bars"));
        Assert.Equal("5", s.Get("BarState-Bar4", "Bars"));
        Assert.Equal("126", s.Get("BarState-Bar6", "BarID"));
        Assert.Null(s.Get("BarState-Bar6", "Floating"));
        Assert.Null(s.Get("BarState-Bar5", "Visible"));
        Assert.Equal("-1500", s.Get("BarState-Bar6", "MRUFloatXPos"));
    }

    [Fact]
    public void FloatingPaneKeepsDockPositionWhenReordered()
    {
        // 入力フォームを左、フォルダ一覧を右にしてからフォルダ一覧を浮かせる
        string path = Path.Combine(_dir, AppSettings.FileName);
        var s = AppSettings.Load(path);
        BarState.Default.MoveTo(127, 0).Float(126, 10, 20, 175, 175).Save(s, 610, 3840, 1200);
        var again = BarState.Load(s)!;
        Assert.Equal([127, 126], again.Panes.Select(p => p.Id));
        Assert.Equal(-2, again.Lefts()[127]);
        Assert.Equal(267, again.ContentLeft());
    }

    [Fact]
    public void ReadsDockingZeroAsFloating()
    {
        // ペインのセクションの Docking=0 と XPos/YPos だけで浮かせた状態を表した ini も読む
        var s = NewSettings();
        BarState.Default.Save(s, 610, 3840, 1200);
        s.Set("BarState-Bar5", "Docking", 0);
        s.Set("BarState-Bar5", "XPos", 900);
        s.Set("BarState-Bar5", "YPos", 100);
        s.Set("BarState-Bar4", "Bars", 3);

        var form = BarState.Load(s)!.Find(127)!;
        Assert.True(form.Floating);
        Assert.Equal((900, 100), (form.FloatX, form.FloatY));
    }

    [Fact]
    public void IgnoresPlaceholderIdsInDockBar()
    {
        // MFC は浮かせたペインの元の場所を ID | 0x10000 の場所取りとして残すことがある
        var s = NewSettings();
        BarState.Default.Float(126, 0, 0, 175, 175).Save(s, 610, 3840, 1200);
        s.Set("BarState-Bar4", "Bars", 5);
        s.Set("BarState-Bar4", "Bar#1", 0x10000 | 126);
        s.Set("BarState-Bar4", "Bar#3", 127);
        s.Set("BarState-Bar4", "Bar#4", 0);
        var state = BarState.Load(s)!;
        Assert.True(state.Find(126)!.Floating);
        Assert.Equal([126, 127], state.Panes.Select(p => p.Id));
    }
}
