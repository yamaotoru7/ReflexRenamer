using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace FrTools;

/// <summary>
/// <c>tools/FrTools/scenarios/*.json</c> に書いた操作手順を実行し、結果を <c>.work/out/&lt;シナリオ&gt;/&lt;アプリ&gt;/</c> に保存する。
/// </summary>
/// <remarks>
/// 形式:
/// <code>
/// {
///   "fixture": "basic",               // .work/fixtures/ に作るフォルダ（起動時に渡す）
///   "steps": [
///     { "do": "command", "id": 65 },                  // WM_COMMAND（オリジナル版のみ。clone では menu を併記する）
///     { "do": "menu", "path": ["表示(V)", "リネーム(R)"] },
///     { "do": "click", "target": "id:1025" },
///     { "do": "toggle", "target": "id:1025", "on": true },
///     { "do": "setText", "target": "id:1010", "text": "*.jpg" },
///     { "do": "select", "target": "id:1146", "item": "コピー" },
///     { "do": "key", "keys": ["CONTROL", "A"] },
///     { "do": "wait", "ms": 500 },
///     { "do": "capture", "name": "main" },            // target 省略時はメインウィンドウ
///     { "do": "tree", "name": "main" },
///     { "do": "readList", "target": "id:1000", "name": "preview" },
///     { "do": "files", "name": "after" }               // フィクスチャ内の実ファイル一覧
///   ]
/// }
/// </code>
/// <c>{ "do": "createFile", "path": "new.txt" }</c> はフィクスチャのフォルダにファイルを作る。
/// <c>{ "do": "drag", "target": "id:126", "at": [dx, dy], "by": [dx, dy] }</c> は相対位置でボタンを押したまま動かす（ペインの幅の変更）。
/// 各ステップに <c>"only": "original"</c> か <c>"only": "clone"</c> を付けると片方だけで実行する。
/// popup の "native": true は Win32 のメニュー位置で子項目を選ぶ。popupItems の "path" は子メニューの内容を記録する。
/// ルートの "scripts": { "Script.vbs": "コード", "Script.js": "コード" } はユーザースクリプトを一時的に差し替える（終了時に復元）。
/// </remarks>
internal static class Scenario
{
    public static string ScenarioDir => Path.Combine(Repo.Root, "tools", "FrTools", "scenarios");

    public static string OutputDir(string scenario, AppKind kind) =>
        Path.Combine(Sandbox.OutDir, scenario, kind.ToString().ToLowerInvariant());

    public static IEnumerable<string> All() =>
        Directory.GetFiles(ScenarioDir, "*.json").Select(Path.GetFileNameWithoutExtension).Order()!;

    public static void Run(string name, AppKind kind)
    {
        string path = Path.Combine(ScenarioDir, name + ".json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;

        string fixture = root.TryGetProperty("fixture", out var f) ? f.GetString()! : "basic";
        string folder = Sandbox.ResetFixture(fixture);
        string outDir = OutputDir(name, kind);
        if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        Directory.CreateDirectory(outDir);

        if (kind == AppKind.Original)
        {
            // 前回の設定が残っていると結果が変わるので、毎回まっさらな作業コピーから始める
            Sandbox.ResetOriginal();
        }

        // "settings": { "セクション": { "キー": "値" } } があれば、起動前に ini に書く（言語の確認など）
        Dictionary<string, Dictionary<string, string>>? settings = null;
        if (root.TryGetProperty("settings", out var settingsElement))
        {
            settings = settingsElement.EnumerateObject().ToDictionary(
                sec => sec.Name,
                sec => sec.Value.EnumerateObject().ToDictionary(kv => kv.Name, kv => kv.Value.GetString() ?? ""));
        }
        using var scriptFixture = new ScriptFixture(kind, root);
        using var session = AppSession.Launch(kind, folder, settings);
        session.Settle(1500);

        int index = 0;
        foreach (var step in root.GetProperty("steps").EnumerateArray())
        {
            index++;
            if (step.TryGetProperty("only", out var only) && !string.Equals(only.GetString(), kind.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string action = step.GetProperty("do").GetString()!;
            try
            {
                ExecuteWithRetry(session, step, action, folder, outDir);
            }
            catch (Exception ex)
            {
                // 失敗時の画面を残しておくと原因を追いやすい
                TryCapture(session, Path.Combine(outDir, $"error-step{index}.png"));
                throw new InvalidOperationException($"[{kind}] ステップ {index}（{action}）で失敗: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// ステップを実行し、失敗したら少し待ってやり直す（最大3回）。
    /// ユーザーが同時に PC を使っていてフォーカスを奪われた場合などに備える。
    /// </summary>
    internal static void ExecuteWithRetry(AppSession s, JsonElement step, string action, string folder, string outDir)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                s.EnsureForeground();
                Execute(s, step, action, folder, outDir);
                AppSession.MarkOwnInput();
                return;
            }
            catch (Exception ex) when (attempt < 3)
            {
                AppSession.MarkOwnInput();
                Console.WriteLine($"  {action} が失敗したのでやり直す（{attempt}回目）: {ex.Message}");
                ClosePopups(s);
                AppSession.WaitForUserIdle();
                Thread.Sleep(1000);
            }
        }
    }

    /// <summary>開いたままのポップアップメニューを閉じる。</summary>
    private static void ClosePopups(AppSession s)
    {
        try
        {
            if (PopupMenus(s).Any())
            {
                s.EnsureForeground();
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                Keyboard.Type(VirtualKeyShort.ESCAPE);
            }
        }
        catch (Exception)
        {
        }
    }

    internal static void Execute(AppSession s, JsonElement step, string action, string folder, string outDir)
    {
        var target = Selector.From(step.TryGetProperty("target", out var t) ? t : null, s.Kind);
        AutomationElement Target() => (target ?? throw new InvalidOperationException("target がない")).Find(s);

        switch (action)
        {
            case "command":
                s.PostCommand(step.GetProperty("id").GetInt32());
                break;

            case "menu":
                InvokeMenu(s, step.GetProperty("path").EnumerateArray().Select(p => p.GetString()!).ToList());
                break;

            case "click":
                // "at": [dx, dy] があれば、対象の左上からの相対位置をマウスでクリックする（オーナードローの一覧の項目など）
                if (step.TryGetProperty("at", out var at))
                {
                    var r = Target().BoundingRectangle;
                    s.EnsureForeground();
                    FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(r.Left + at[0].GetInt32(), r.Top + at[1].GetInt32()));
                    // "button": "right" で右クリック（一覧の見出しのコンテキストメニューなど）
                    bool right = step.TryGetProperty("button", out var button) && button.GetString() == "right";
                    // "double": true でダブルクリック（ペインのつまみ・フローティングのタイトルバーなど）
                    if (step.TryGetProperty("double", out var dbl) && dbl.GetBoolean()) FlaUI.Core.Input.Mouse.DoubleClick();
                    else FlaUI.Core.Input.Mouse.Click(right ? FlaUI.Core.Input.MouseButton.Right : FlaUI.Core.Input.MouseButton.Left);
                    AppSession.MarkOwnInput();
                }
                else
                {
                    Activate(Target());
                }
                s.Settle();
                break;

            case "drag":
            {
                // 対象の左上からの相対位置 "at": [dx, dy] でボタンを押し、"by": [dx, dy] だけ動かして放す（ペインの幅の変更など）
                var r = Target().BoundingRectangle;
                var drag = step.GetProperty("at");
                var by = step.GetProperty("by");
                var from = new System.Drawing.Point(r.Left + drag[0].GetInt32(), r.Top + drag[1].GetInt32());
                var to = new System.Drawing.Point(from.X + by[0].GetInt32(), from.Y + by[1].GetInt32());
                s.EnsureForeground();
                FlaUI.Core.Input.Mouse.MoveTo(from);
                Thread.Sleep(200);
                FlaUI.Core.Input.Mouse.Down(FlaUI.Core.Input.MouseButton.Left);
                for (int i = 1; i <= 10; i++)
                {
                    FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(from.X + (to.X - from.X) * i / 10, from.Y + (to.Y - from.Y) * i / 10));
                    Thread.Sleep(30);
                }
                FlaUI.Core.Input.Mouse.Up(FlaUI.Core.Input.MouseButton.Left);
                AppSession.MarkOwnInput();
                s.Settle();
                break;
            }

            case "toggle":
            {
                var e = Target();
                bool on = step.GetProperty("on").GetBoolean();
                var toggle = e.Patterns.Toggle.Pattern;
                if ((toggle.ToggleState.Value == ToggleState.On) != on) toggle.Toggle();
                s.Settle();
                break;
            }

            case "setText":
            {
                var e = Target();
                string text = step.GetProperty("text").GetString()!;
                // ComboBox の場合は中の Edit に入れる
                var edit = e.ControlType == ControlType.ComboBox ? e.FindFirstChild(cf => cf.ByControlType(ControlType.Edit)) ?? e : e;
                // ValuePattern（WM_SETTEXT）では EN_CHANGE が飛ばずプレビューが更新されないので、キー入力で打ち込む
                edit.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.Press(VirtualKeyShort.DELETE);
                Keyboard.Release(VirtualKeyShort.DELETE);
                if (text.Length > 0)
                {
                    Keyboard.Type(text);
                }
                s.Settle();
                // 途中でフォーカスを奪われると入力が欠けるので、入ったかを確かめる（やり直しの対象にする）。
                // アプリがプレビューの更新中だと反映が遅れるので、少し待ちながら読み直す
                string? actual = null;
                for (int i = 0; i < 10; i++)
                {
                    actual = edit.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
                    if (actual is null || actual == text) break;
                    Thread.Sleep(300);
                }
                if (actual is not null && actual != text)
                {
                    throw new InvalidOperationException($"入力が一致しない（期待 '{text}'、実際 '{actual}'）");
                }
                break;
            }

            case "select":
            {
                // Select だけでは Win32 のコンボボックスに CBN_SELCHANGE が飛ばないので、開いて項目をクリックする
                var combo = Target().AsComboBox();
                string text = step.GetProperty("item").GetString()!;
                var items = ComboListItems(s, combo);
                int index = Array.FindIndex(items.ToArray(), i => i.Name == text);
                if (index < 0)
                {
                    throw new InvalidOperationException($"コンボボックスに '{text}' がない");
                }
                // 移植版（WinForms）ではドロップダウンの項目の位置が信用できず、クリックが別の場所（ツールバー）に
                // 届くことがあるので、閉じてキーボードで選ぶ。オリジナル版はクリックで選ぶ（CBN_SELCHANGE が飛ぶ）
                if (s.Kind == AppKind.Clone)
                {
                    combo.Collapse();
                    s.Settle(200);
                    combo.Focus();
                    Keyboard.Type(VirtualKeyShort.HOME);
                    for (int i = 0; i < index; i++)
                    {
                        Keyboard.Type(VirtualKeyShort.DOWN);
                    }
                    s.Settle();
                }
                else
                {
                    items[index].Click();
                    s.Settle();
                }
                if (ComboValue(combo) != text)
                {
                    try
                    {
                        items[index].Patterns.SelectionItem.PatternOrDefault?.Select();
                    }
                    catch (Exception)
                    {
                    }
                    s.Settle();
                }
                if (ComboValue(combo) != text)
                {
                    combo.Collapse();
                    combo.Focus();
                    Keyboard.Type(VirtualKeyShort.HOME);
                    for (int i = 0; i < index; i++)
                    {
                        Keyboard.Type(VirtualKeyShort.DOWN);
                    }
                    s.Settle();
                }
                if (ComboValue(combo) != text)
                {
                    throw new InvalidOperationException($"コンボボックスで '{text}' を選べない（現在 '{ComboValue(combo)}'）");
                }
                break;
            }

            case "key":
            {
                var keys = step.GetProperty("keys").EnumerateArray()
                    .Select(k => Enum.Parse<VirtualKeyShort>(k.GetString()!.Length == 1 ? "KEY_" + k.GetString() : k.GetString()!, ignoreCase: true))
                    .ToArray();
                // 既定は同時押し（Ctrl+A など）。"each": true なら1つずつ順に押す（メニューの ↓ → Enter など）
                if (step.TryGetProperty("each", out var each) && each.GetBoolean())
                {
                    foreach (var k in keys)
                    {
                        Keyboard.Type(k);
                        Thread.Sleep(250);
                    }
                }
                else
                {
                    Keyboard.TypeSimultaneously(keys);
                }
                s.Settle();
                break;
            }

            case "wait":
                Thread.Sleep(step.GetProperty("ms").GetInt32());
                break;

            case "capture":
                s.Capture(target is null ? s.Main : Target(), Path.Combine(outDir, Name(step) + ".png"));
                break;

            case "tree":
                File.WriteAllText(Path.Combine(outDir, Name(step) + ".tree.txt"), UiTree.Dump(target is null ? s.Main : Target()));
                break;

            case "readList":
                File.WriteAllText(Path.Combine(outDir, Name(step) + ".list.json"), ReadList(Target(), CurrentFolder(s)));
                break;

            case "readValue":
            {
                var element = Target();
                var edit = element.ControlType == ControlType.ComboBox
                    ? element.FindFirstChild(cf => cf.ByControlType(ControlType.Edit)) ?? element
                    : element;
                string value = edit.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault
                    ?? throw new InvalidOperationException("入力値を取得できない");
                File.WriteAllText(Path.Combine(outDir, Name(step) + ".value.txt"), value);
                break;
            }

            case "treeSelect":
            {
                // ツリーの項目を名前で探し、見える位置までスクロールしてクリックする
                string text = step.GetProperty("item").GetString()!;
                var item = Target().FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                    .FirstOrDefault(i => i.Name == text)
                    ?? throw new InvalidOperationException($"ツリーに '{text}' がない");
                item.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView();
                Thread.Sleep(200);
                item.Click();
                s.Settle();
                break;
            }

            case "expandAll":
                ExpandAll(Target());
                s.Settle();
                break;

            case "comboItems":
            {
                var combo = Target().AsComboBox();
                var items = ComboListItems(s, combo).Select(i => i.Name ?? "").ToList();
                combo.Collapse();
                File.WriteAllText(Path.Combine(outDir, Name(step) + ".items.txt"), string.Join('\n', items) + "\n");
                s.Settle();
                break;
            }

            case "popup":
            {
                // ボタンを押して出たポップアップメニューから項目を選ぶ。path の途中はサブメニュー
                OpenPopup(s, Target());
                if (step.TryGetProperty("native", out var native) && native.GetBoolean())
                {
                    nint menu = NativePopup.Handle(PopupMenus(s).First().Properties.NativeWindowHandle.Value);
                    var labels = step.GetProperty("path").EnumerateArray().Select(p => p.GetString()!).ToArray();
                    for (int i = 0; i < labels.Length; i++)
                    {
                        var items = NativePopup.Read(menu).Where(item => !item.Separator).ToList();
                        int index = items.FindIndex(item => MenuLabelMatches(item.Text, labels[i]));
                        if (index < 0) throw new InvalidOperationException($"メニュー項目 '{labels[i]}' がない: {string.Join(" / ", items.Select(item => item.Text))}");
                        FlaUI.Core.Input.Mouse.MoveTo(NativePopup.Point(menu, items[index].Position));
                        if (i == labels.Length - 1) FlaUI.Core.Input.Mouse.Click();
                        else
                        {
                            menu = items[index].Submenu;
                            if (menu == 0) throw new InvalidOperationException($"'{labels[i]}' は子メニューではない");
                            Thread.Sleep(900);
                        }
                        Thread.Sleep(300);
                    }
                    s.Settle();
                    break;
                }
                var path = step.GetProperty("path").EnumerateArray().Select(p => p.GetString()!).ToList();
                for (int i = 0; i < path.Count; i++)
                {
                    // まず名前で項目を探す（同じアクセスキーの項目が複数あるメニューに備える）。
                    // 見つからなければアクセスキー（末尾の「(C)」など）で選ぶ。サブメニューの項目は UI Automation から見えないため
                    var named = FindPopupItem(s, path[i]);
                    char? key = named is null ? AccessKey(path[i]) : null;
                    if (key is { } k)
                    {
                        s.EnsureForeground();
                        Keyboard.Type(k);
                        Thread.Sleep(500);
                        // 同じアクセスキーの項目が複数あると（MENU 173 の「タグ・リネーム(T)」と「タイムスタンプ(T)」）
                        // 選択が移るだけで実行されない。最後の項目でメニューがまだ開いていれば Enter で確定する
                        if (i == path.Count - 1 && PopupMenus(s).Any())
                        {
                            Keyboard.Type(VirtualKeyShort.RETURN);
                            Thread.Sleep(500);
                        }
                        continue;
                    }
                    var item = WaitFor(() => FindPopupItem(s, path[i]), $"ポップアップの項目 '{path[i]}'", () => PopupItemNames(s)).AsMenuItem();
                    if (i < path.Count - 1)
                    {
                        item.Click();
                        Thread.Sleep(900);
                    }
                    else
                    {
                        item.Click();
                    }
                }
                s.Settle();
                break;
            }

            case "popupItems":
            {
                // ボタンを押して出たポップアップメニューの項目を記録し、閉じる
                OpenPopup(s, Target());
                if (step.TryGetProperty("path", out var submenuPath))
                {
                    nint menu = NativePopup.Handle(PopupMenus(s).First().Properties.NativeWindowHandle.Value);
                    foreach (var part in submenuPath.EnumerateArray())
                    {
                        string label = part.GetString()!;
                        var item = NativePopup.Read(menu).FirstOrDefault(i => MenuLabelMatches(i.Text, label))
                            ?? throw new InvalidOperationException($"子メニュー '{label}' が見つからない");
                        menu = item.Submenu;
                        if (menu == 0) throw new InvalidOperationException($"'{label}' は子メニューではない");
                    }
                    var items = NativePopup.Read(menu).Where(i => !i.Separator)
                        .Select(i => i.Text.Replace("&", "") + (i.Checked ? " [チェック]" : "") + (i.Enabled ? "" : " [無効]"));
                    File.WriteAllText(Path.Combine(outDir, Name(step) + ".popup.txt"), string.Join('\n', items) + "\n");
                    Keyboard.Type(VirtualKeyShort.ESCAPE);
                    s.Settle();
                    break;
                }
                var lines = PopupMenus(s)
                    .SelectMany(m => m.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem)))
                    .Select(m => DescribeMenuItem(m));
                File.WriteAllText(Path.Combine(outDir, Name(step) + ".popup.txt"), string.Join('\n', lines) + "\n");
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                s.Settle();
                break;
            }

            case "files":
                File.WriteAllText(Path.Combine(outDir, Name(step) + ".files.txt"), ListFiles(folder));
                break;

            case "createFile":
                // フィクスチャのフォルダにファイルを作る（フォルダの監視の確認）
                File.WriteAllText(Path.Combine(folder, step.GetProperty("path").GetString()!), "x");
                break;

            case "contextSelect":
            {
                // すでに開いているポップアップメニューから項目を選ぶ（popup と同じ path の規則）
                var deadline = DateTime.UtcNow.AddMilliseconds(2000);
                while (!PopupMenus(s).Any() && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(100);
                }
                var path = step.GetProperty("path").EnumerateArray().Select(p => p.GetString()!).ToList();
                for (int i = 0; i < path.Count; i++)
                {
                    var named = FindPopupItem(s, path[i]);
                    char? key = named is null ? AccessKey(path[i]) : null;
                    if (key is { } k)
                    {
                        s.EnsureForeground();
                        Keyboard.Type(k);
                        Thread.Sleep(500);
                        if (i == path.Count - 1 && PopupMenus(s).Any())
                        {
                            Keyboard.Type(VirtualKeyShort.RETURN);
                            Thread.Sleep(500);
                        }
                        continue;
                    }
                    var item = WaitFor(() => FindPopupItem(s, path[i]), $"ポップアップの項目 '{path[i]}'", () => PopupItemNames(s)).AsMenuItem();
                    if (i < path.Count - 1)
                    {
                        // サブメニューは Invoke では開かないので、項目にマウスを重ねて開く（クリックすると閉じることがある）
                        FlaUI.Core.Input.Mouse.MoveTo(item.GetClickablePoint());
                        Thread.Sleep(900);
                    }
                    else
                    {
                        item.Invoke();
                    }
                }
                s.Settle();
                break;
            }

            case "contextItems":
            {
                // すでに開いているポップアップメニュー（右クリックで出したものなど）の項目を記録し、閉じる
                var deadline = DateTime.UtcNow.AddMilliseconds(2000);
                while (!PopupMenus(s).Any() && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(100);
                }
                var lines = PopupMenus(s)
                    .SelectMany(m => m.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem)))
                    .Select(m => DescribeMenuItem(m));
                File.WriteAllText(Path.Combine(outDir, Name(step) + ".popup.txt"), string.Join('\n', lines) + "\n");
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                s.Settle();
                break;
            }

            default:
                throw new InvalidOperationException($"未知の操作: {action}");
        }
    }

    private static string Name(JsonElement step) => step.GetProperty("name").GetString()!;

    private static string? ComboValue(FlaUI.Core.AutomationElements.ComboBox combo)
    {
        try
        {
            return combo.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? combo.SelectedItem?.Name;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// コンボボックスを開いて項目を取る。WinForms のコンボボックスは UI Automation の Items に項目が出ないので、
    /// 開いたときに出るリスト窓（クラス ComboLBox、デスクトップ直下）から取る。
    /// </summary>
    private static IReadOnlyList<AutomationElement> ComboListItems(AppSession s, FlaUI.Core.AutomationElements.ComboBox combo)
    {
        combo.Expand();
        Thread.Sleep(300);
        var items = combo.Items;
        if (items.Length > 0)
        {
            return items;
        }
        // GetComboBoxInfo でドロップダウンのリスト窓のハンドルを取り、その子（ListItem）を読む
        var info = new ComboBoxInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<ComboBoxInfo>() };
        if (!GetComboBoxInfo(combo.Properties.NativeWindowHandle.Value, ref info) || info.List == 0)
        {
            throw new InvalidOperationException("コンボボックスのリスト窓が取れない");
        }
        var list = s.Automation.FromHandle(info.List);
        return WaitFor(() => list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)) is { Length: > 0 } found ? found : null, "コンボボックスの項目");
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int Size;
        public System.Drawing.Rectangle Item;
        public System.Drawing.Rectangle Button;
        public int ButtonState;
        public nint Combo;
        public nint Edit;
        public nint List;
    }

    [System.Runtime.InteropServices.DllImport("user32")]
    private static extern bool GetComboBoxInfo(nint hwnd, ref ComboBoxInfo info);

    /// <summary>
    /// 要素をマウスでクリックする。ラジオボタンは取りこぼすことがあるので、選ばれるまでクリックし直す。
    /// UI Automation の Select や Invoke は使わない（MSAA 経由の既定動作を使うと、その後ポップアップメニューが開かなくなる。
    /// またポップアップを出すボタンを Invoke すると、メニューが閉じるまで戻らない）。
    /// </summary>
    public static void Activate(AutomationElement e)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            bool isRadio = e.ControlType == ControlType.RadioButton;
            try
            {
                e.Click();
            }
            catch (System.Runtime.InteropServices.COMException) when (!isRadio)
            {
                // ボタンを押した結果ダイアログが閉じると、クリック後の問い合わせが失敗する（0x80040201）。クリック自体は済んでいる
                return;
            }
            if (!isRadio) return;
            Thread.Sleep(300);
            if (e.Patterns.SelectionItem.PatternOrDefault?.IsSelected.ValueOrDefault == true) return;
        }
        throw new InvalidOperationException("ラジオボタンが選択できない");
    }

    /// <summary>
    /// ボタンを押してポップアップメニューを開く。マウスのクリックで開かないことがあるので、
    /// そのときはフォーカスを当ててスペースキーで押し直す。
    /// </summary>
    private static void OpenPopup(AppSession s, AutomationElement button)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt == 0)
            {
                button.Click();
            }
            else
            {
                button.Focus();
                Keyboard.Type(VirtualKeyShort.SPACE);
            }
            var deadline = DateTime.UtcNow.AddMilliseconds(1500);
            while (DateTime.UtcNow < deadline)
            {
                if (PopupMenus(s).Any())
                {
                    Thread.Sleep(200);
                    return;
                }
                Thread.Sleep(100);
            }
        }
        var tops = s.Automation.GetDesktop().FindAllChildren().Take(15)
            .Select(e => $"{e.Properties.ClassName.ValueOrDefault}:{e.Properties.Name.ValueOrDefault}");
        throw new InvalidOperationException("ポップアップメニューが開かない。デスクトップ直下: " + string.Join(" / ", tops));
    }

    /// <summary>表示中のポップアップメニュー（クラス #32768 のトップレベルウィンドウ）。</summary>
    private static IEnumerable<AutomationElement> PopupMenus(AppSession s) =>
        s.Automation.GetDesktop().FindAllChildren(cf => cf.ByClassName("#32768"));

    private static AutomationElement? FindPopupItem(AppSession s, string label) =>
        PopupMenus(s)
            .SelectMany(m => m.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem)))
            .FirstOrDefault(m => MenuLabelMatches(SafeName(m), label));

    /// <summary>名前が取れない項目（区切りなど）は空として扱う。</summary>
    private static string? SafeName(AutomationElement m)
    {
        try
        {
            return m.Properties.Name.ValueOrDefault;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string DescribeMenuItem(AutomationElement m)
    {
        string state = "";
        try
        {
            if (m.Patterns.Toggle.TryGetPattern(out var toggle) && toggle.ToggleState.Value == ToggleState.On) state = " [チェック]";
        }
        catch (Exception)
        {
        }
        if (!m.IsEnabled) state += " [無効]";
        return (m.Name ?? "") + state;
    }

    private static void ExpandAll(AutomationElement tree)
    {
        // 展開すると子が増えるので、増えなくなるまで繰り返す
        for (int pass = 0; pass < 10; pass++)
        {
            bool expanded = false;
            foreach (var item in tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
            {
                var pattern = item.Patterns.ExpandCollapse.PatternOrDefault;
                if (pattern?.ExpandCollapseState.ValueOrDefault == ExpandCollapseState.Collapsed)
                {
                    pattern.Expand();
                    expanded = true;
                }
            }
            if (!expanded) break;
            Thread.Sleep(200);
        }
    }

    private static void InvokeMenu(AppSession s, List<string> path)
    {
        var menuBar = s.Main.FindFirstDescendant(cf => cf.ByControlType(ControlType.MenuBar))
            ?? throw new InvalidOperationException("メニューバーがない");
        AutomationElement current = menuBar;
        foreach (var (label, i) in path.Select((p, i) => (p, i)))
        {
            // サブメニューはメインウィンドウの外のポップアップに出るので、デスクトップ全体から探す
            var scope = i == 0 ? current : s.Automation.GetDesktop();
            var item = WaitFor(() => scope.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem))
                .FirstOrDefault(m => MenuLabelMatches(m.Name, label)), $"メニュー項目 '{label}'");
            if (i == path.Count - 1)
            {
                // Invoke はモーダルダイアログを開く項目で応答待ちのまま時間切れになるので、マウスでクリックする
                try
                {
                    item.Click();
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }
                AppSession.MarkOwnInput();
            }
            else
            {
                item.AsMenuItem().Expand();
            }
            Thread.Sleep(300);
            current = item;
        }
        s.Settle();
    }

    /// <summary>ラベルの一致判定。アクセラレータ表記（タブ以降）は無視する。</summary>
    private static bool MenuLabelMatches(string? name, string label)
    {
        if (name is null) return false;
        return NormalizeLabel(name) == NormalizeLabel(label);
    }

    /// <summary>ラベル末尾の「(C)」のようなアクセスキー。なければ null。</summary>
    private static char? AccessKey(string label)
    {
        var m = System.Text.RegularExpressions.Regex.Match(label.Split('\t')[0].Trim(), @"\(&?([A-Za-z0-9])\)$");
        return m.Success ? char.ToLowerInvariant(m.Groups[1].Value[0]) : null;
    }

    /// <summary>アクセラレータ表記（タブ以降）、&amp;、末尾のアクセスキー「(M)」を除く。</summary>
    private static string NormalizeLabel(string s)
    {
        string n = s.Split('\t')[0].Replace("&", "").Trim();
        return System.Text.RegularExpressions.Regex.Replace(n, @"\([A-Za-z0-9]\)$", "").Trim();
    }

    private static T WaitFor<T>(Func<T?> find, string what, Func<string>? detail = null) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var r = find();
            if (r is not null) return r;
            Thread.Sleep(200);
        }
        throw new InvalidOperationException($"{what} が見つからない" + (detail is null ? "" : $"（{detail()}）"));
    }

    /// <summary>表示中のポップアップメニューの項目名（失敗時の説明用）。</summary>
    private static string PopupItemNames(AppSession s) =>
        "項目: " + string.Join(" / ", PopupMenus(s).SelectMany(m => m.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem))).Select(m => SafeName(m) ?? "(名前なし)"));

    /// <summary>リストビューの全行を、列見出しをキーにした JSON で返す。</summary>
    public static string ReadList(AutomationElement list, string? baseFolder = null)
    {
        var headers = list.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem)).Select(h => h.Name).ToList();
        var rows = new List<Dictionary<string, string>>();
        foreach (var item in list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)))
        {
            var cells = item.FindAllChildren();
            var row = new Dictionary<string, string>();
            for (int i = 0; i < cells.Length; i++)
            {
                string key = i < headers.Count ? headers[i] : $"col{i}";
                row[key] = cells[i].Name ?? "";
                if (key == "フォルダ" && baseFolder is not null)
                {
                    row[key] = RelativeFolder(baseFolder, row[key]);
                }
            }
            if (cells.Length == 0)
            {
                row[headers.FirstOrDefault() ?? "col0"] = item.Name ?? "";
            }
            rows.Add(row);
        }
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>フォルダ欄（1003）の値。読めなければ null。</summary>
    private static string? CurrentFolder(AppSession s)
    {
        try
        {
            var box = new Selector("id:1003,type:ComboBox").Find(s, TimeSpan.FromSeconds(2));
            var edit = box.FindFirstChild(cf => cf.ByControlType(ControlType.Edit)) ?? box;
            return edit.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 移植版の一覧は「フォルダ」列をフォルダ欄からの相対パスで出す（FolderDisplay.Relative）。
    /// 両方の値を同じ規則でそろえて比べる（フルパスで出ているものだけ変換する）。
    /// </summary>
    private static string RelativeFolder(string baseFolder, string folder)
    {
        if (!Path.IsPathRooted(folder) || !Path.IsPathRooted(baseFolder))
        {
            return folder;
        }
        string relative = Path.GetRelativePath(baseFolder, folder);
        return Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("..\\", StringComparison.Ordinal) ? folder : relative;
    }

    /// <summary>フォルダの中身。名前（フォルダは末尾に /）、更新日時、作成日時、属性をタブ区切りで並べる（属性やタイムスタンプの実行結果の照合に使う）。</summary>
    private static string ListFiles(string folder) =>
        string.Join('\n', Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories)
            .Select(p =>
            {
                bool dir = Directory.Exists(p);
                FileSystemInfo info = dir ? new DirectoryInfo(p) : new FileInfo(p);
                var attrs = info.Attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive);
                // 実行で作られたばかりのものは時刻が毎回変わるので「(now)」にする
                string Time(DateTime t) => (DateTime.Now - t).Duration() < TimeSpan.FromHours(1) ? "(now)" : t.ToString("yyyy-MM-dd HH:mm:ss");
                string size = dir ? "" : ((FileInfo)info).Length.ToString();
                return $"{Path.GetRelativePath(folder, p).Replace('\\', '/')}{(dir ? "/" : "")}\t{Time(info.LastWriteTime)}\t{Time(info.CreationTime)}\t{attrs}\t{size}";
            })
            .Order(StringComparer.Ordinal)) + "\n";

    private static void TryCapture(AppSession s, string path)
    {
        try
        {
            s.Capture(s.Main, path);
        }
        catch (Exception)
        {
        }
    }
}
