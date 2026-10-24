namespace ReflexRenamer.Core.Presets;

/// <summary>
/// リネームモード。値は <c>Preset.ini</c> の Mode 列に対応する。
/// </summary>
public enum RenameMode
{
    /// <summary>ワイルドカード（<c>*</c>、<c>?</c>）</summary>
    Wildcard = 0,

    /// <summary>正規表現</summary>
    RegularExpression = 1,

    /// <summary>文字範囲の置換（<c>tr</c> 相当）</summary>
    CharacterRange = 2,
}

/// <summary>
/// <c>[Preset]</c> の1行。形式は <c>Mode\tScript\tSearch\tReplace\tName</c>。
/// </summary>
public sealed record Preset(RenameMode Mode, string Script, string Search, string Replace, string Name);

/// <summary>
/// <c>[Typical]</c>（定型処理）の1行。形式は <c>Param\tCategory\tMode\tScript\tSearch\tReplace\tName</c>。
/// </summary>
/// <param name="Param">テンプレートが使う入力（<c>\ADD</c>、<c>\NUM</c>、<c>\POS</c> など）を示すビットマスク。</param>
/// <param name="Category">UI 上の分類番号。</param>
public sealed record TypicalPreset(
    int Param,
    int Category,
    RenameMode Mode,
    string Script,
    string Search,
    string Replace,
    string Name);
