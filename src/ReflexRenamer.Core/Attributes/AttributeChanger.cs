using ReflexRenamer.Core.Rename;

namespace ReflexRenamer.Core.Attributes;

/// <summary>タイムスタンプの変え方（属性ページのコンボボックス）。</summary>
public enum TimestampMode
{
    /// <summary>日時指定：チェックした項目を指定の日時にする。</summary>
    Specify,

    /// <summary>シフト：日数と時間だけ前または後にずらす。</summary>
    Shift,

    /// <summary>ランダム生成：下限と上限の間の日時にする。</summary>
    Random,

    /// <summary>連続生成：開始日時から順に、間隔ずつ進める。</summary>
    Sequential,

    /// <summary>揃える：元となる日時（作成・更新・アクセス・撮影日）を適用先にコピーする。</summary>
    MakeSame,
}

public enum TimestampKind
{
    Created,
    Updated,
    Accessed,

    /// <summary>EXIF の撮影日時（揃えるの元としてのみ）。</summary>
    PhotoTaken,
}

/// <summary>属性の変え方。3状態のチェックボックスに対応する。</summary>
public enum AttributeChange
{
    Keep,
    Set,
    Clear,
}

public sealed record AttributeOptions
{
    public TimestampMode Mode { get; init; } = TimestampMode.Specify;

    // 日時指定：null は変更しない
    public DateTime? Created { get; init; }

    public DateTime? Updated { get; init; }

    public DateTime? Accessed { get; init; }

    // シフト
    public bool ShiftEarlier { get; init; }

    public int ShiftDays { get; init; }

    public TimeSpan ShiftTime { get; init; }

    // ランダム生成・連続生成
    public DateTime RangeMin { get; init; } = DateTime.Today;

    public DateTime RangeMax { get; init; } = DateTime.Today;

    public DateTime SequenceStart { get; init; } = DateTime.Today;

    public TimeSpan SequenceStep { get; init; } = TimeSpan.FromSeconds(1);

    // 揃える
    public TimestampKind BaseKind { get; init; } = TimestampKind.Created;

    /// <summary>シフト・ランダム・連続・揃えるの適用先。</summary>
    public bool ApplyToCreated { get; init; }

    public bool ApplyToUpdated { get; init; } = true;

    public bool ApplyToAccessed { get; init; }

    public bool ModifyFolderTimestamp { get; init; }

    public AttributeChange ReadOnly { get; init; }

    public AttributeChange Archive { get; init; }

    public AttributeChange Hidden { get; init; }

    public AttributeChange System { get; init; }

    /// <summary>EXIF の撮影日時を読む関数。null なら撮影日は使えない。</summary>
    public Func<string, DateTime?>? PhotoTakenReader { get; init; }

    public Random Random { get; init; } = Random.Shared;
}

/// <summary>1ファイルに対する変更内容（プレビューにも使う）。null は変更しない。</summary>
public sealed record AttributePlan(FileItem Item, DateTime? Created, DateTime? Updated, DateTime? Accessed, FileAttributes Attributes);

/// <summary>
/// 属性ページの処理：タイムスタンプと属性の変更。
/// </summary>
public static class AttributeChanger
{
    /// <summary>各ファイルに適用する値を決める。連続生成は一覧の順に進める。</summary>
    public static List<AttributePlan> Plan(IReadOnlyList<FileItem> items, AttributeOptions o)
    {
        var plans = new List<AttributePlan>(items.Count);
        DateTime sequence = o.SequenceStart;
        foreach (var item in items)
        {
            if (item.IsDirectory && !o.ModifyFolderTimestamp)
            {
                plans.Add(new AttributePlan(item, null, null, null, NewAttributes(item, o)));
                continue;
            }

            DateTime? created = null, updated = null, accessed = null;
            switch (o.Mode)
            {
                case TimestampMode.Specify:
                    created = o.Created;
                    updated = o.Updated;
                    accessed = o.Accessed;
                    break;
                case TimestampMode.Shift:
                {
                    var delta = TimeSpan.FromDays(o.ShiftDays) + o.ShiftTime;
                    if (o.ShiftEarlier) delta = -delta;
                    Apply(o, t => t + delta, item, out created, out updated, out accessed);
                    break;
                }
                case TimestampMode.Random:
                {
                    long span = Math.Max((o.RangeMax - o.RangeMin).Ticks, 0);
                    var value = o.RangeMin + TimeSpan.FromTicks(span == 0 ? 0 : o.Random.NextInt64(span + 1));
                    Apply(o, _ => value, item, out created, out updated, out accessed);
                    break;
                }
                case TimestampMode.Sequential:
                {
                    // 連続生成も「前に／後に」に従う。既定の「前に」では開始日時から過去へ進む（exec-attribute-seq の実測）
                    var value = sequence;
                    sequence = o.ShiftEarlier ? sequence - o.SequenceStep : sequence + o.SequenceStep;
                    Apply(o, _ => value, item, out created, out updated, out accessed);
                    break;
                }
                case TimestampMode.MakeSame:
                {
                    DateTime? source = o.BaseKind switch
                    {
                        TimestampKind.Created => item.Created,
                        TimestampKind.Updated => item.Modified,
                        TimestampKind.Accessed => item.Accessed,
                        _ => o.PhotoTakenReader?.Invoke(item.FullPath),
                    };
                    if (source is { } s)
                    {
                        Apply(o, _ => s, item, out created, out updated, out accessed);
                    }
                    break;
                }
            }
            plans.Add(new AttributePlan(item, created, updated, accessed, NewAttributes(item, o)));
        }
        return plans;
    }

    private static void Apply(AttributeOptions o, Func<DateTime, DateTime> f, FileItem item, out DateTime? created, out DateTime? updated, out DateTime? accessed)
    {
        created = o.ApplyToCreated ? f(item.Created) : null;
        updated = o.ApplyToUpdated ? f(item.Modified) : null;
        accessed = o.ApplyToAccessed ? f(item.Accessed) : null;
    }

    private static FileAttributes NewAttributes(FileItem item, AttributeOptions o)
    {
        var a = File.GetAttributes(item.FullPath);
        a = Change(a, FileAttributes.ReadOnly, o.ReadOnly);
        a = Change(a, FileAttributes.Archive, o.Archive);
        a = Change(a, FileAttributes.Hidden, o.Hidden);
        a = Change(a, FileAttributes.System, o.System);
        return a;
    }

    private static FileAttributes Change(FileAttributes a, FileAttributes flag, AttributeChange change) => change switch
    {
        AttributeChange.Set => a | flag,
        AttributeChange.Clear => a & ~flag,
        _ => a,
    };

    /// <summary>計画を適用する。失敗した件数を返す。</summary>
    public static int Execute(IEnumerable<AttributePlan> plans, Func<double, bool>? progress = null)
    {
        int failed = 0;
        var list = plans.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (progress is not null && !progress((double)i / list.Count))
            {
                break;
            }
            var p = list[i];
            try
            {
                string path = p.Item.FullPath;
                // 読み取り専用のままだとタイムスタンプを変えられないので、属性は先に外してから最後に設定する
                var current = File.GetAttributes(path);
                if ((current & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(path, current & ~FileAttributes.ReadOnly);
                }
                if (p.Item.IsDirectory)
                {
                    if (p.Created is { } c) Directory.SetCreationTime(path, c);
                    if (p.Updated is { } u) Directory.SetLastWriteTime(path, u);
                    if (p.Accessed is { } a) Directory.SetLastAccessTime(path, a);
                }
                else
                {
                    if (p.Created is { } c) File.SetCreationTime(path, c);
                    if (p.Updated is { } u) File.SetLastWriteTime(path, u);
                    if (p.Accessed is { } a) File.SetLastAccessTime(path, a);
                }
                File.SetAttributes(path, p.Attributes);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }
        progress?.Invoke(1);
        return failed;
    }
}
