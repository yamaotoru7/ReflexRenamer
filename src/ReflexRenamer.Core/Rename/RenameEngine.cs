using System.Text;
using System.Text.RegularExpressions;
using ReflexRenamer.Core.Presets;

namespace ReflexRenamer.Core.Rename;

/// <summary>プレビュー一覧の1行。</summary>
public sealed record PreviewRow(FileItem Item, string NewName);

/// <summary>
/// 高度なリネームの処理本体。設定を一度コンパイルし、ファイル一覧からプレビューを作る。
/// </summary>
/// <remarks>
/// 置換は3段階で行う（実測から判明）。
/// <list type="number">
/// <item>パターン置換（<see cref="ReplacementFormatter"/>）。文字変換モードでは <see cref="Translator"/></item>
/// <item>ファイル情報・日時のタグ展開（<see cref="TagExpander"/>）</item>
/// <item>連番とランダム数字（<see cref="NumberExpander"/>）。スクリプトはこの後で評価する</item>
/// </list>
/// 検索は絞り込みも兼ねる。ワイルドカードは名前全体に一致したもの、正規表現は空でないマッチがあるものだけを一覧に残す。
/// 文字変換はすべてのファイルを残す。
/// </remarks>
public sealed class RenameEngine
{
    private readonly RenameOptions _options;
    private readonly Regex? _regex;
    private readonly Translator? _translator;
    private readonly bool _invalid;

    public RenameEngine(RenameOptions options)
    {
        _options = options;
        if (options.Search.Length == 0)
        {
            return;
        }

        switch (options.Mode)
        {
            case RenameMode.Wildcard:
                _regex = WildcardPattern.ToRegex(options.Search);
                break;
            case RenameMode.RegularExpression:
                _regex = BoostRegex.TryCreate(options.Search, options.CaseSensitive);
                _invalid = _regex is null;
                break;
            case RenameMode.CharacterRange:
                _translator = new Translator(options.Search, options.Replace);
                break;
        }
    }

    /// <summary>
    /// 一覧の順にプレビューを作る。対象外のファイルは結果に含まれない。
    /// 連番は結果に残った行の順に振る。
    /// </summary>
    public List<PreviewRow> Preview(IEnumerable<FileItem> items)
    {
        var rows = new List<PreviewRow>();
        if (_invalid)
        {
            return rows;
        }

        // フォルダごとにリセットするときはフォルダ別に数える
        var numbers = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            string replacement = _options.TagExpander is { } expandTags ? expandTags(item, _options.Replace) : _options.Replace;
            string? replaced = ApplyPattern(item.Name, replacement);
            if (replaced is null)
            {
                continue;
            }

            string key = (_options.NumberGroupKey?.Invoke(item) ?? "") + "|" + (_options.ResetNumberByFolder ? item.DirectoryPath : "");
            long number = numbers.TryGetValue(key, out var n) ? n : _options.NumberStart;
            numbers[key] = number + _options.NumberStep;

            string newName = replaced;
            if (!ReferenceEquals(replaced, item.Name))
            {
                // 連番はスクリプトより先に展開する（オリジナル版の実測：golden script-order の so-number-before、so-number-start）
                string? expanded = NumberExpander.Expand(TagExpander.Expand(replaced, item, _options), number, _options, randomDigits: _options.Script is null);
                if (_options.Script is { } script)
                {
                    expanded = EvaluateScript(script, expanded);
                }
                newName = expanded is null ? item.Name : ReplaceInvalidChars(expanded);
            }
            rows.Add(new PreviewRow(item, newName));
        }
        return rows;
    }

    /// <summary>スクリプトを評価する。エラーなら null（オリジナル版はその項目の名前を変えない。explore-typical の digit：ＡＢＣ１２３.txt）。</summary>
    private static string? EvaluateScript(Func<string, string> script, string code)
    {
        try
        {
            return script(code);
        }
        catch (Scripting.ScriptException)
        {
            return null;
        }
    }

    /// <summary>
    /// ファイル名に使えない文字を置き換える（実測：<c>\</c> は <c>%</c>、<c>:</c> は <c>;</c>）。
    /// <c>/</c> はフォルダ振り分けに使うので残す。
    /// </summary>
    private static string ReplaceInvalidChars(string name) =>
        name.Replace('\\', '%').Replace(':', ';');

    /// <summary>
    /// 1段目の置換。対象外なら null、変更しない場合は元の文字列そのもの（参照が同じ）を返す。
    /// </summary>
    private string? ApplyPattern(string name, string replacement)
    {
        if (_options.Search.Length == 0)
        {
            return name;
        }

        switch (_options.Mode)
        {
            case RenameMode.Wildcard:
            {
                var m = _regex!.Match(name);
                if (!m.Success)
                {
                    return null;
                }
                // 置換が空なら名前を変えない（実測）
                return replacement.Length == 0 ? name : ReplacementFormatter.Format(replacement, m, missingGroupAsDigit: true);
            }

            case RenameMode.RegularExpression:
                return ReplaceRegex(name, replacement);

            case RenameMode.CharacterRange:
                return _translator!.Translate(name);
        }
        return name;
    }

    private string? ReplaceRegex(string name, string replacement)
    {
        var sb = new StringBuilder();
        int last = 0;
        bool any = false;
        for (var m = _regex!.Match(name); m.Success; m = m.NextMatch())
        {
            // 空のマッチは無視する（boost の match_not_null 相当）
            if (m.Length == 0)
            {
                continue;
            }
            if (_options.CompleteMatchOnly && (m.Index != 0 || m.Length != name.Length))
            {
                continue;
            }
            any = true;
            sb.Append(name, last, m.Index - last);
            sb.Append(ReplacementFormatter.Format(replacement, m, missingGroupAsDigit: false));
            last = m.Index + m.Length;
        }
        if (!any)
        {
            return null;
        }
        sb.Append(name, last, name.Length - last);
        return sb.ToString();
    }
}
