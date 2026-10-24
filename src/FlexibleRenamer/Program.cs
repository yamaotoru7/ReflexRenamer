using FlexibleRenamer.Ui;

namespace FlexibleRenamer;

static class Program
{
    /// <summary>
    /// 起動する。引数にフォルダを渡すと、そのフォルダを開いた状態で始まる（オリジナル版と同じ）。
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // 画面の処理中の例外は exe と同じ場所の error.log に書く（自動操作中にメッセージボックスで止まらないように）
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject as Exception);
        var resources = AppResources.Load();
        // 「送る」のショートカットは /Rename /TagRename /Attribute /NumberedObject でページを指定する。それ以外の引数はフォルダ（またはファイル）
        int? page = null;
        string? target = null;
        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "/rename": page = Commands.PageRename; break;
                case "/tagrename": page = Commands.PageTagRename; break;
                case "/attribute": page = Commands.PageAttribute; break;
                case "/numberedobject": page = Commands.PageNumberedObject; break;
                default: target ??= arg; break;
            }
        }
        Application.Run(new MainForm(resources, target, page));
    }

    private static void LogError(Exception? e)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "error.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
