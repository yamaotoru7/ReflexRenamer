using System.Globalization;

namespace FlexibleRenamer.Core.Settings;

/// <summary>
/// ペイン（コントロールバー）1つの状態。<paramref name="Width"/> は左にドッキングしたときの幅（ini の sizeVertCX）。
/// </summary>
public sealed record DockedPane(int Id, int Width)
{
    /// <summary>浮かせたことがない位置（MFC の CW_USEDEFAULT、ini では -2147483648）。</summary>
    public const int Unspecified = int.MinValue;

    /// <summary>浮かせている（小さなツールウィンドウに入っている）。</summary>
    public bool Floating { get; init; }

    /// <summary>閉じるボタンなどで隠している（ini の Visible=0）。</summary>
    public bool Hidden { get; init; }

    /// <summary>浮かせたウィンドウの左上（スクリーン座標、ini の MRUFloatXPos/YPos）。</summary>
    public int FloatX { get; init; } = Unspecified;

    public int FloatY { get; init; }

    /// <summary>浮かせたときのペインの大きさ（ini の sizeFloatCX/CY）。0 なら既定値。</summary>
    public int FloatWidth { get; init; }

    public int FloatHeight { get; init; }

    /// <summary>左の列に表示されている。</summary>
    public bool InDock => !Floating && !Hidden;
}

/// <summary>
/// ドッキングペイン（フォルダ一覧 126、入力フォーム 127）の配置。オリジナル版は MFC の SaveBarState と
/// CSizingControlBar（[BarState-SCBar-ID]）で ini に書くので、同じ形式で読み書きする（A21）。
/// <list type="bullet">
/// <item><c>[BarState-Summary]</c>：Bars（セクション数）、ScreenCX/CY。</item>
/// <item><c>[BarState-BarN]</c>：BarID=59420（左のドックバー）が並び順を持つ。Bar#n が上から（縦置きでは左から）の並びで、0 は列の区切り。</item>
/// <item><c>[BarState-BarN]</c>：BarID=59423（浮かせたウィンドウの中のドックバー）。Floating=1、Horz、XPos/YPos がウィンドウの位置、Bar#n が中のペイン。</item>
/// <item><c>[BarState-BarN]</c>：BarID=126／127 がペインの状態（Visible、XPos/YPos、MRUDock*Pos、MRUFloat*）。</item>
/// <item><c>[BarState-SCBar-ID]</c>：sizeVertCX が左に置いたときの幅、sizeVertCY が高さ、sizeFloatCX/CY が浮かせたときの大きさ。</item>
/// </list>
/// 移植版が扱うのは左へのドッキングと浮かせた状態だけ（上下や右へのドッキングは既定の位置に戻す）。
/// </summary>
public sealed class BarState
{
    public const int FolderTreeId = 126;
    public const int InputFormId = 127;

    /// <summary>AFX_IDW_DOCKBAR_LEFT。</summary>
    public const int LeftDockBarId = 59420;

    /// <summary>AFX_IDW_DOCKBAR_FLOAT（CMiniDockFrameWnd の中のドックバー）。</summary>
    public const int FloatDockBarId = 59423;

    /// <summary>オリジナル版が Bar0〜3 に書くツールバー類の BarID（実測。59392 は2つある）。</summary>
    private static readonly int[] ToolBarIds = [59392, 59392, 59396, 59393];

    /// <summary>オリジナル版の MRUFloatStyle（0x2004 = CBRS_ALIGN_TOP | CBRS_FLOAT_MULTI）。</summary>
    public const int FloatStyle = 8196;

    /// <summary>ペインどうし、ペインとファイル一覧は 2px 重なる（オリジナル版の実測：126 の右端 394 と 127 の XPos 392）。</summary>
    public const int Overlap = 2;

    /// <summary>左端のペインはドックバーの 2px 外から始まる（XPos=-2、YPos=-2）。</summary>
    public const int Origin = -2;

    /// <summary>CSizingControlBar の最小の大きさ（m_szMinVert の既定値 33×32）。</summary>
    public const int MinWidth = 33;

    /// <summary>MFC がドックバーの Bar# で一時的な場所取り（浮かせる前の位置）に付けるビット。</summary>
    private const int PlaceholderMask = 0xFFFF;

    private const string Prefix = "BarState-";

    /// <summary>MFC が SaveState で書くキー（セクションを書き直す前に消す）。</summary>
    private static readonly string[] BarKeys =
    [
        "BarID", "Visible", "Horz", "Floating", "XPos", "YPos", "MRUWidth", "Docking", "MRUDockID",
        "MRUDockLeftPos", "MRUDockTopPos", "MRUDockRightPos", "MRUDockBottomPos",
        "MRUFloatStyle", "MRUFloatXPos", "MRUFloatYPos", "Bars",
    ];

    public BarState(IEnumerable<DockedPane> panes) => Panes = panes.ToList();

    /// <summary>
    /// 左から順。浮かせたペインや隠したペインも、ドッキングに戻したときの位置としてこの並びに残る。
    /// </summary>
    public IReadOnlyList<DockedPane> Panes { get; }

    /// <summary>ini がないとき（とレイアウトのリセット）の配置。幅はオリジナル版の起動直後の実測。</summary>
    public static BarState Default { get; } = new([new(FolderTreeId, 175), new(InputFormId, 271)]);

    public static int DefaultWidth(int id) => id == FolderTreeId ? 175 : 271;

    /// <summary>上下に置いたとき・浮かせたときの高さの既定値（オリジナル版の ini の sizeHorzCY、sizeFloatCY）。</summary>
    private static int DefaultHorzHeight(int id) => id == FolderTreeId ? 175 : 503;

    public DockedPane? Find(int id) => Panes.FirstOrDefault(p => p.Id == id);

    public int WidthOf(int id) => Find(id)?.Width ?? DefaultWidth(id);

    /// <summary>浮かせたときのペインの大きさ（未設定なら既定値）。</summary>
    public (int Width, int Height) FloatSizeOf(int id)
    {
        var p = Find(id);
        return (p is { FloatWidth: > 0 } ? p.FloatWidth : DefaultWidth(id),
                p is { FloatHeight: > 0 } ? p.FloatHeight : DefaultHorzHeight(id));
    }

    public BarState With(int id, Func<DockedPane, DockedPane> change) =>
        new(Panes.Select(p => p.Id == id ? change(p) : p));

    public BarState WithWidth(int id, int width) => With(id, p => p with { Width = width });

    /// <summary>ペインを浮かせる。位置と大きさは浮かせたウィンドウ（左上はスクリーン座標、大きさはペイン）。</summary>
    public BarState Float(int id, int x, int y, int width, int height) =>
        With(id, p => p with { Floating = true, FloatX = x, FloatY = y, FloatWidth = width, FloatHeight = height });

    /// <summary>浮かせたペインを元の位置（並びの中の位置）にドッキングする。</summary>
    public BarState Dock(int id) => With(id, p => p with { Floating = false });

    public BarState SetHidden(int id, bool hidden) => With(id, p => p with { Hidden = hidden });

    /// <summary>ペインを並びの <paramref name="index"/> 番目へ移す。</summary>
    public BarState MoveTo(int id, int index)
    {
        var list = Panes.ToList();
        int from = list.FindIndex(p => p.Id == id);
        if (from < 0)
        {
            return this;
        }
        var pane = list[from];
        list.RemoveAt(from);
        list.Insert(Math.Clamp(index, 0, list.Count), pane);
        return new(list);
    }

    /// <summary>
    /// ini から読む。BarState がなければ null。
    /// 左のドックバーにあるペインはその順に、浮かせたウィンドウにあるペインは浮かせた状態にする。
    /// ペインのセクションが Docking=0 のときも浮かせたものとして XPos/YPos をウィンドウの位置に使う。
    /// どこにもないペインは、ドッキングに戻す位置（MRUDockLeftPos）か既定の並びの位置に置く。
    /// </summary>
    public static BarState? Load(AppSettings settings)
    {
        int bars = settings.GetInt(Prefix + "Summary", "Bars", -1);
        if (bars < 0)
        {
            return null;
        }
        var known = Default.Panes.Select(p => p.Id).ToHashSet();
        var order = new List<int>();
        var floating = new Dictionary<int, (int X, int Y)>();
        var paneSections = new Dictionary<int, string>();
        for (int i = 0; i < bars; i++)
        {
            string section = BarSection(i);
            int barId = settings.GetInt(section, "BarID", 0);
            bool floatBar = barId == FloatDockBarId || settings.GetBool(section, "Floating", false);
            if (known.Contains(barId))
            {
                paneSections[barId] = section;
            }
            if (barId != LeftDockBarId && !floatBar)
            {
                continue;
            }
            foreach (int id in BarIds(settings, section))
            {
                if (!known.Contains(id) || order.Contains(id) || floating.ContainsKey(id))
                {
                    continue;
                }
                if (floatBar)
                {
                    floating[id] = (settings.GetInt(section, "XPos", DockedPane.Unspecified), settings.GetInt(section, "YPos", 0));
                }
                else
                {
                    order.Add(id);
                }
            }
        }

        var panes = new Dictionary<int, DockedPane>();
        foreach (int id in known)
        {
            string sc = ScBarSection(id);
            var pane = new DockedPane(id, Math.Max(MinWidth, settings.GetInt(sc, "sizeVertCX", DefaultWidth(id))))
            {
                FloatWidth = settings.GetInt(sc, "sizeFloatCX", 0),
                FloatHeight = settings.GetInt(sc, "sizeFloatCY", 0),
            };
            if (paneSections.TryGetValue(id, out string? s))
            {
                pane = pane with
                {
                    Hidden = !settings.GetBool(s, "Visible", true),
                    FloatX = settings.GetInt(s, "MRUFloatXPos", DockedPane.Unspecified),
                    FloatY = settings.GetInt(s, "MRUFloatYPos", 0),
                };
                if (!order.Contains(id) && !floating.ContainsKey(id) && !settings.GetBool(s, "Docking", true))
                {
                    floating[id] = (settings.GetInt(s, "XPos", DockedPane.Unspecified), settings.GetInt(s, "YPos", 0));
                }
            }
            if (floating.TryGetValue(id, out var pos))
            {
                pane = pane with { Floating = true, FloatX = pos.X, FloatY = pos.Y };
            }
            panes[id] = pane;
        }

        // 左の列にないペインは、ドッキングに戻す位置（MRUDockLeftPos）が左の列のペインの位置以下ならその前に置く（浮かせたペインは幅を取らないので同じ値になる）
        int DockLeft(int id) => paneSections.TryGetValue(id, out string? s)
            ? settings.GetInt(s, order.Contains(id) ? "XPos" : "MRUDockLeftPos", int.MaxValue)
            : int.MaxValue;
        foreach (var (pane, index) in Default.Panes.Select((p, i) => (p, i)))
        {
            if (order.Contains(pane.Id))
            {
                continue;
            }
            int left = DockLeft(pane.Id);
            int at = left == int.MaxValue
                ? Math.Min(index, order.Count)
                : order.FindIndex(id => DockLeft(id) >= left) is var found && found >= 0 ? found : order.Count;
            order.Insert(at, pane.Id);
        }
        return new(order.Select(id => panes[id]));
    }

    /// <summary>ドックバーの Bar# の並び（0 の区切りと場所取りを除く）。</summary>
    private static IEnumerable<int> BarIds(AppSettings settings, string section)
    {
        int count = settings.GetInt(section, "Bars", 0);
        for (int n = 0; n < count; n++)
        {
            int id = settings.GetInt(section, "Bar#" + n.ToString(CultureInfo.InvariantCulture), 0);
            if (id != 0 && id == (id & PlaceholderMask))
            {
                yield return id;
            }
        }
    }

    /// <summary>
    /// ini に書く。<paramref name="dockHeight"/> はペインを置く領域の高さ（ペインは上下に 2px はみ出すので sizeVertCY はこれより 4 大きい）。
    /// </summary>
    public void Save(AppSettings settings, int dockHeight, int screenWidth, int screenHeight, Func<int, bool>? visible = null)
    {
        int cy = dockHeight - 2 * Origin;
        foreach (var pane in Panes)
        {
            string sc = ScBarSection(pane.Id);
            var (fw, fh) = FloatSizeOf(pane.Id);
            // 上下に置いたときの大きさはこのアプリでは変わらないので、前の値（なければ既定値）を残す
            settings.Set(sc, "sizeHorzCX", settings.GetInt(sc, "sizeHorzCX", DefaultWidth(pane.Id)));
            settings.Set(sc, "sizeHorzCY", settings.GetInt(sc, "sizeHorzCY", DefaultHorzHeight(pane.Id)));
            settings.Set(sc, "sizeVertCX", pane.Width);
            settings.Set(sc, "sizeVertCY", cy);
            settings.Set(sc, "sizeFloatCX", pane.FloatWidth > 0 ? fw : settings.GetInt(sc, "sizeFloatCX", fw));
            settings.Set(sc, "sizeFloatCY", pane.FloatHeight > 0 ? fh : settings.GetInt(sc, "sizeFloatCY", fh));
        }

        // 前回のセクションを消してから書く（MFC も WriteProfileString(section, NULL, NULL) で消す）
        int oldBars = settings.GetInt(Prefix + "Summary", "Bars", 0);
        for (int i = 0; i < oldBars; i++)
        {
            ClearBarSection(settings, BarSection(i));
        }

        int bar = 0;
        foreach (int id in ToolBarIds)
        {
            settings.Set(BarSection(bar++), "BarID", id);
        }
        // 左のドックバー：オリジナル版はペインごとに列を分ける（0, 126, 0, 127, 0）。浮かせたペインは入れない
        string dock = BarSection(bar++);
        settings.Set(dock, "BarID", LeftDockBarId);
        var docked = Panes.Where(p => !p.Floating).ToList();
        if (docked.Count > 0)
        {
            WriteBarIds(settings, dock, docked.Select(p => p.Id));
        }
        // 浮かせたウィンドウ（CMiniDockFrameWnd）の中のドックバー
        foreach (var pane in Panes.Where(p => p.Floating))
        {
            string s = BarSection(bar++);
            settings.Set(s, "BarID", FloatDockBarId);
            settings.Set(s, "Horz", 1);
            settings.Set(s, "Floating", 1);
            settings.Set(s, "XPos", pane.FloatX);
            settings.Set(s, "YPos", pane.FloatY);
            WriteBarIds(settings, s, [pane.Id]);
        }
        // ペインの状態。オリジナル版は 127、126 の順に書く。浮かせたペインの MRUDock*Pos はドッキングに戻したときの位置
        var lefts = DockLefts();
        foreach (var pane in Panes.OrderByDescending(p => p.Id))
        {
            string s = BarSection(bar++);
            int x = lefts[pane.Id];
            settings.Set(s, "BarID", pane.Id);
            if (pane.Hidden || visible?.Invoke(pane.Id) == false)
            {
                settings.Set(s, "Visible", 0);
            }
            settings.Set(s, "XPos", pane.Floating ? Origin : x);
            settings.Set(s, "YPos", Origin);
            settings.Set(s, "Docking", 1);
            settings.Set(s, "MRUDockID", pane.Floating ? LeftDockBarId : 0);
            settings.Set(s, "MRUDockLeftPos", x);
            settings.Set(s, "MRUDockTopPos", Origin);
            settings.Set(s, "MRUDockRightPos", x + pane.Width);
            settings.Set(s, "MRUDockBottomPos", Origin + cy);
            settings.Set(s, "MRUFloatStyle", FloatStyle);
            settings.Set(s, "MRUFloatXPos", pane.FloatX);
            settings.Set(s, "MRUFloatYPos", pane.FloatY);
        }
        settings.Set(Prefix + "Summary", "Bars", bar);
        settings.Set(Prefix + "Summary", "ScreenCX", screenWidth);
        settings.Set(Prefix + "Summary", "ScreenCY", screenHeight);
    }

    private static void WriteBarIds(AppSettings settings, string section, IEnumerable<int> ids)
    {
        var list = ids.ToList();
        settings.Set(section, "Bars", list.Count * 2 + 1);
        settings.Set(section, "Bar#0", 0);
        for (int i = 0; i < list.Count; i++)
        {
            settings.Set(section, "Bar#" + (i * 2 + 1).ToString(CultureInfo.InvariantCulture), list[i]);
            settings.Set(section, "Bar#" + (i * 2 + 2).ToString(CultureInfo.InvariantCulture), 0);
        }
    }

    private static void ClearBarSection(AppSettings settings, string section)
    {
        int count = settings.GetInt(section, "Bars", 0);
        foreach (string key in BarKeys.Concat(Enumerable.Range(0, count).Select(n => "Bar#" + n.ToString(CultureInfo.InvariantCulture))))
        {
            settings.SetList(section, key, []);
        }
    }

    /// <summary>
    /// すべてのペインを左の列に置いたときの左端（浮かせたペインのドッキングに戻す位置）。表示中のペインの位置は <see cref="Lefts"/> と同じ。
    /// </summary>
    private Dictionary<int, int> DockLefts()
    {
        var result = new Dictionary<int, int>();
        int x = Origin;
        foreach (var pane in Panes)
        {
            result[pane.Id] = x;
            if (pane.InDock)
            {
                x += pane.Width - Overlap;
            }
        }
        return result;
    }

    /// <summary>
    /// 左の列に表示中のペインの左端（ドック領域の座標）。左端のペインは -2 から、次のペインは前のペインの右端の 2px 手前から始まる。
    /// 浮かせたペイン、隠したペイン、<paramref name="visible"/> が false のペインは除く。
    /// </summary>
    public IReadOnlyDictionary<int, int> Lefts(Func<int, bool>? visible = null)
    {
        var result = new Dictionary<int, int>();
        int x = Origin;
        foreach (var pane in Shown(visible))
        {
            result[pane.Id] = x;
            x += pane.Width - Overlap;
        }
        return result;
    }

    /// <summary>ファイル一覧の左端（最後のペインの右端の 2px 手前。ペインがなければ 0）。</summary>
    public int ContentLeft(Func<int, bool>? visible = null)
    {
        var shown = Shown(visible).ToList();
        return shown.Count == 0 ? 0 : Origin + shown.Sum(p => p.Width - Overlap);
    }

    private IEnumerable<DockedPane> Shown(Func<int, bool>? visible) =>
        Panes.Where(p => p.InDock && (visible?.Invoke(p.Id) ?? true));

    /// <summary>
    /// ペインの幅をドラッグで変えるときの幅。<paramref name="minWidth"/> 以上で、ファイル一覧が
    /// <paramref name="minContent"/> px 以上残る範囲に収める。
    /// </summary>
    public int ClampWidth(int id, int requested, int dockWidth, int minWidth, int minContent, Func<int, bool>? visible = null)
    {
        int others = ContentLeft(visible) - WidthOf(id);
        int max = dockWidth - minContent - others;
        return Math.Max(minWidth, Math.Min(requested, max));
    }

    private static string BarSection(int index) => Prefix + "Bar" + index.ToString(CultureInfo.InvariantCulture);

    private static string ScBarSection(int id) => Prefix + "SCBar-" + id.ToString(CultureInfo.InvariantCulture);
}
