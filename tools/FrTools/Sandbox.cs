namespace FrTools;

/// <summary>
/// 自動操作用の作業領域（<c>.work/</c>、git 管理外）を用意する。
/// オリジナル版は実行ディレクトリに ini と log を書くため、<c>FlexRenamer/</c> ではなくコピーを起動する。
/// </summary>
internal static class Sandbox
{
    public static string WorkDir => Path.Combine(Repo.Root, ".work");

    public static string OriginalDir => Path.Combine(WorkDir, "sandbox", "original");

    public static string OriginalExe => Path.Combine(OriginalDir, "Flexible Renamer.exe");

    public static string CloneExe =>
        Path.Combine(Repo.Root, "src", "FlexibleRenamer", "bin", "Debug", "net10.0-windows", "FlexibleRenamer.exe");

    public static string FixturesDir => Path.Combine(WorkDir, "fixtures");

    public static string OutDir => Path.Combine(WorkDir, "out");

    /// <summary>
    /// オリジナル版をまっさらな状態（ini・log なし）で作業領域にコピーする。
    /// </summary>
    public static void ResetOriginal()
    {
        // 前回の実行が残っていると exe を消せないので、先に終了させる
        AppSession.KillExisting(OriginalExe);
        if (Directory.Exists(OriginalDir))
        {
            // 外部エディタが作業ディレクトリとして保持していても再作成できるよう、ルート自体は残す。
            foreach (var file in Directory.GetFiles(OriginalDir))
            {
                File.Delete(file);
            }
            foreach (var directory in Directory.GetDirectories(OriginalDir))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        CopyDirectory(Path.Combine(Repo.Root, "FlexRenamer"), OriginalDir);
        foreach (var stateFile in new[] { "Flexible Renamer.ini", "Rename.log" })
        {
            File.Delete(Path.Combine(OriginalDir, stateFile));
        }
    }

    /// <summary>
    /// テスト用のフォルダを作り直す。タイムスタンプも固定するので、何度作っても同じ状態になる。
    /// </summary>
    public static string ResetFixture(string name)
    {
        if (!Fixtures.All.TryGetValue(name, out var entries))
        {
            throw new ArgumentException($"フィクスチャ '{name}' は定義されていない。候補: {string.Join(", ", Fixtures.All.Keys)}");
        }

        string dir = Path.Combine(FixturesDir, name);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
        Directory.CreateDirectory(dir);

        // 親フォルダのタイムスタンプは子を作るたびに変わるので、作り終えてから深い順に設定する
        var stamps = new List<(string Path, DateTime Time, DateTime Created, bool IsDir)>();
        foreach (var e in entries)
        {
            string path = Path.Combine(dir, e.Path);
            if (e.Path.EndsWith('/'))
            {
                Directory.CreateDirectory(path);
                stamps.Add((path.TrimEnd('/'), e.Time, e.CreatedTime, true));
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, e.Content ?? new byte[e.Size]);
                stamps.Add((path, e.Time, e.CreatedTime, false));
            }
        }
        foreach (var (path, time, created, isDir) in stamps.OrderByDescending(s => s.Path.Length))
        {
            if (isDir)
            {
                Directory.SetCreationTime(path, created);
                Directory.SetLastWriteTime(path, time);
                Directory.SetLastAccessTime(path, time);
            }
            else
            {
                File.SetCreationTime(path, created);
                File.SetLastWriteTime(path, time);
                File.SetLastAccessTime(path, time);
            }
        }
        return dir;
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
        foreach (var sub in Directory.GetDirectories(from))
        {
            CopyDirectory(sub, Path.Combine(to, Path.GetFileName(sub)));
        }
    }
}

/// <summary>フィクスチャの1項目。Time は更新日時とアクセス日時、Created を省略すると作成日時も Time にする。</summary>
internal sealed record FixtureEntry(string Path, DateTime Time, int Size = 0, DateTime? Created = null, byte[]? Content = null)
{
    public DateTime CreatedTime => Created ?? Time;
}

/// <summary>
/// テスト用フォルダの定義。パス末尾が <c>/</c> ならフォルダ。
/// </summary>
internal static class Fixtures
{
    private static DateTime T(string s) => DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    public static readonly Dictionary<string, FixtureEntry[]> All = new()
    {
        ["basic"] =
        [
            new("DSC0001.JPG", T("2003-01-15 10:20:30"), 1024),
            new("DSC0002.jpg", T("2003-01-15 14:05:06"), 2048),
            new("DSC0003.jpeg", T("2003-01-16 09:00:00"), 3000),
            new("photo (1).jpg", T("2003-01-17 23:59:59"), 100),
            new("photo [2].jpg", T("2003-01-17 00:00:01"), 100),
            new("Copy of report.txt", T("2010-05-01 12:00:00"), 10),
            new("report - Copy (2).txt", T("2010-05-02 12:00:00"), 10),
            new("hello world.TXT", T("2011-03-06 08:30:00"), 5),
            new("a-b.c-d.txt", T("2011-03-06 08:30:00"), 5),
            new("README", T("2009-12-31 23:59:59"), 1),
            new("テスト ファイル.txt", T("2008-03-09 01:02:03"), 7),
            new("ﾃｽﾄｶﾅ.txt", T("2008-03-09 01:02:04"), 7),
            new("ＡＢＣ１２３.txt", T("2008-03-09 01:02:05"), 7),
            new("song10.mp3", T("2007-06-03 06:09:02"), 0),
            new("song2.mp3", T("2007-06-03 06:09:03"), 0),
            new("sub1/", T("2005-05-05 05:05:05")),
            new("sub1/inner.txt", T("2005-05-05 05:05:06"), 3),
            new("サブフォルダ/", T("2006-06-06 06:06:06")),
        ],
        // ドットやハイフンを多く含む名前（ワイルドカードの一致規則を調べる）
        ["dots"] =
        [
            new("x.y.z", T("2001-01-01 01:01:01"), 1),
            new("v1.2.3.tar.gz", T("2001-01-01 01:01:02"), 1),
            new("a.b-c.d", T("2001-01-01 01:01:03"), 1),
            new("a-b-c.txt", T("2001-01-01 01:01:04"), 1),
            new("a-b.c-d.txt", T("2001-01-01 01:01:05"), 1),
            new("photo.jpg.bak", T("2001-01-01 01:01:06"), 1),
            new("noext", T("2001-01-01 01:01:07"), 1),
            new(".hidden", T("2001-01-01 01:01:08"), 1),
            new("aaa", T("2001-01-01 01:01:09"), 1),
        ],
        // 定型メニューの各項目が対象にする名前
        ["typical"] =
        [
            new("コピー ～ 報告.txt", T("2012-01-01 10:00:00"), 1),
            new("コピー (2) ～ 報告.txt", T("2012-01-02 10:00:00"), 1),
            new("報告 - コピー.txt", T("2012-01-03 10:00:00"), 1),
            new("報告 - コピー (3).txt", T("2012-01-04 10:00:00"), 1),
            new("メモ へのショートカット.txt", T("2012-01-05 10:00:00"), 1),
            new("メモ - ショートカット.txt", T("2012-01-06 10:00:00"), 1),
            new("photo (1).jpg", T("2012-01-07 10:00:00"), 1),
            new("photo [2].jpg", T("2012-01-08 10:00:00"), 1),
            new("写真【3】.jpg", T("2012-01-09 10:00:00"), 1),
            new("写真（4）.jpg", T("2012-01-10 10:00:00"), 1),
            new("a(1)b[2].txt", T("2012-01-11 10:00:00"), 1),
            new("averylongfilename.html", T("2012-01-12 10:00:00"), 1),
            new("noext_long_name", T("2012-01-13 10:00:00"), 1),
            new("hello world.TXT", T("2012-01-14 10:00:00"), 1),
            new("can't stop.mp3", T("2012-01-15 10:00:00"), 1),
            new("Café Ñoño.txt", T("2012-01-16 10:00:00"), 1),
            new("ひらがな.txt", T("2012-01-17 10:00:00"), 1),
            new("カタカナ.txt", T("2012-01-18 10:00:00"), 1),
            new("ﾊﾝｶｸｶﾅ.txt", T("2012-01-19 10:00:00"), 1),
            new("track1-disc02.mp3", T("2012-01-20 10:00:00"), 1),
            new("IMG_0042.JPG", T("2012-01-21 10:00:00"), 1),
            new("a.b.c.txt", T("2012-01-22 10:00:00"), 1),
            new("README", T("2012-01-23 10:00:00"), 1),
        ],
        // タグ付きのファイル（タグ・リネームの観察用）
        ["tags"] =
        [
            new("song1.mp3", T("2012-03-04 05:06:07"), Content: FixtureContent.Mp3("Opening Song", "The Band", "First Album", "2005", 1, "(17)", "nice")),
            // 日本語は ANSI（CP932）で入れる。オリジナル版は UTF-16 の日本語を正しく表示できない（features.md C06）
            new("song2.mp3", T("2012-03-04 05:06:08"), Content: FixtureContent.Mp3("曲その2: 旅/帰郷", "歌手A", "アルバム*名", "2006", 12, "Jazz", "", unicode: false)),
            new("song3.mp3", T("2012-03-04 05:06:13"), Content: FixtureContent.Mp3("Only V1", "Solo", "Lone Album", "1999", 7, "(0)", "c", withV2: false)),
            new("photo1.jpg", T("2012-03-04 05:06:09"), Content: FixtureContent.Jpeg("Canon", "EOS 5D", "2010:11:12 13:14:15", 1, (1u, 250u), 400)),
            new("photo2.jpg", T("2012-03-04 05:06:10"), Content: FixtureContent.Jpeg("NIKON", "D700", "2011:01:02 03:04:05", 0, (2u, 1u), 100)),
            new("page.htm", T("2012-03-04 05:06:11"), Content: FixtureContent.Html("Page Title")),  // Web ページの拡張子は htm;stm;shtml（html は含まれない）
            new("doc.pdf", T("2012-03-04 05:06:12"), Content: FixtureContent.Pdf("PDF Title", "Writer")),
            new("plain.txt", T("2012-03-04 05:06:13"), 3),
        ],
        // WMA・APE・IPTC の合成ファイル（T402、T404。オリジナル版にタグを読ませて照合する）
        ["tags2"] =
        [
            new("wma1.wma", T("2013-04-05 06:07:01"), Content: FixtureContent.Wma("WMA Title", "WMA Artist", "wma comment",
                ("WM/AlbumTitle", "WMA Album"), ("WM/Year", "2004"), ("WM/Genre", "Rock"), ("WM/TrackNumber", 7u))),
            new("wma2.wma", T("2013-04-05 06:07:02"), Content: FixtureContent.Wma("Second", "Other", "",
                ("WM/AlbumTitle", "Album 2"), ("WM/TrackNumber", "12"), ("WM/Genre", "Jazz"))),
            new("wma3.wma", T("2013-04-05 06:07:03"), Content: FixtureContent.Wma("Track Only", "X", "", ("WM/Track", 4u))),
            new("ape1.ape", T("2013-04-05 06:07:04"), Content: FixtureContent.Ape(false,
                ("Title", "APE Title"), ("Artist", "APE Artist"), ("Album", "APE Album"), ("Track", "3"), ("Year", "2007"), ("Genre", "Pop"), ("Comment", "ape c"))),
            new("ape2.ape", T("2013-04-05 06:07:05"), Content: FixtureContent.Ape(true,
                ("Title", "With Header"), ("Artist", "H"), ("Track", "11/12"))),
            new("mpc1.mpc", T("2013-04-05 06:07:06"), Content: FixtureContent.Ape(false, ("Title", "MPC Title"), ("Artist", "MPC Artist"))),
            new("ogg1.ogg", T("2013-04-05 06:07:12"), Content: FixtureContent.Ogg(
                "TITLE=OGG Title", "ARTIST=OGG Artist", "ALBUM=OGG Album", "TRACKNUMBER=5", "DATE=2009", "GENRE=Ambient", "COMMENT=ogg c")),
            // オリジナル版は画像としてデコードできる JPEG からしか IPTC を読まない（APP13 だけの最小 JPEG、EXIF だけの合成 JPEG、
            // 合成 PSD では何も出ない。2026-10-24 の実測）。そのため System.Drawing で作った本物の JPEG に差し込む
            new("iptc5.jpg", T("2013-04-05 06:07:11"), Content: FixtureContent.InsertIptc(
                FixtureContent.RealJpeg(), (0, "\u0000\u0002"), (5, "Real Object"), (15, "Cat"), (55, "20240315"), (80, "Photographer"), (85, "Staff"),
                (90, "Osaka"), (95, "Kansai"), (101, "Japan"), (103, "REF1"), (105, "Headline"), (110, "Credit"), (116, "(c) Someone"),
                (120, "R caption"), (122, "Writer"))),
        ],
        // OLE 複合文書の文書情報（T405。Windows の構造化ストレージで作った本物の複合文書）
        ["docs"] =
        [
            new("doc1.doc", T("2014-05-06 07:08:01"), Content: OleFixture.Build(new("Doc Title", "Doc Subject", "Doc Author", "k1 k2", "doc comment",
                "Microsoft Word 8.0", "ACME", T("2010-01-02 03:04:05"), T("2011-06-07 08:09:10")))),
            new("sheet1.xls", T("2014-05-06 07:08:02"), Content: OleFixture.Build(new("Sheet Title", "", "Sheet Author", "", "",
                "Microsoft Excel", "", T("2012-12-31 23:59:58"), T("2013-01-01 00:00:01")))),
            new("doc2.doc", T("2014-05-06 07:08:03"), Content: OleFixture.Build(new("Unicode Doc", "", "", "", "", "", "Unicode Co",
                T("2010-01-02 03:04:05"), T("2011-06-07 08:09:10"), UnicodeDocSummary: true))),
            new("paper.pdf", T("2014-05-06 07:08:04"), Content: FixtureContent.WellFormedPdf("PDF Title", "PDF Author", "PDF Subject", "pk1",
                "Writer App", "PDF Lib", "D:20200102030405+09'00'", "D:20210304050607Z")),
        ],
        // サブフォルダと、作成日時が更新日時と違うファイル（オプションの挙動を調べる）
        ["tree"] =
        [
            new("b.txt", T("2020-01-02 03:04:05"), 10, T("2019-12-31 23:00:00")),
            new("a.jpg", T("2020-02-03 04:05:06"), 20, T("2018-07-08 09:10:11")),
            new("C.TXT", T("2020-03-04 05:06:07"), 30, T("2017-01-01 00:00:00")),
            new("dir1/", T("2015-01-01 00:00:00")),
            new("dir1/x.txt", T("2021-01-01 01:01:01"), 1),
            new("dir1/y.jpg", T("2021-01-01 01:01:02"), 2),
            new("dir1/deep/", T("2015-01-01 00:00:00")),
            new("dir1/deep/z.txt", T("2022-02-02 02:02:02"), 3),
            new("dir2/", T("2016-01-01 00:00:00")),
            new("dir2/w.txt", T("2023-03-03 03:03:03"), 4),
        ],
    };
}
