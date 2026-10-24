using System.Runtime.InteropServices;

namespace FlexibleRenamer.Ui;

/// <summary>
/// ファイルの右クリックメニュー。エクスプローラーと同じ動詞（開く、プロパティなど）を Shell.Application の Verbs から取って出す。
/// </summary>
public static class ShellContextMenu
{
    public static void Show(Form owner, AppResources res, string path, Point screen)
    {
        dynamic? shell = null;
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null)
            {
                return;
            }
            shell = Activator.CreateInstance(type)!;
            dynamic? folder = shell.NameSpace(Path.GetDirectoryName(path));
            dynamic? item = folder?.ParseName(Path.GetFileName(path));
            if (item is null)
            {
                return;
            }
            dynamic verbs = item.Verbs();
            var popup = new PopupMenu(res);
            int count = verbs.Count;
            var keep = new List<object>();
            for (int i = 0; i < count; i++)
            {
                dynamic verb = verbs.Item(i);
                string name = verb.Name;
                if (name.Length == 0)
                {
                    popup.AddSeparator();
                    continue;
                }
                var v = verb;
                keep.Add(v);
                popup.AddItem(name, () =>
                {
                    try
                    {
                        v.DoIt();
                    }
                    catch (COMException)
                    {
                    }
                });
            }
            popup.ShowAt(owner, screen);
        }
        catch (Exception e) when (e is COMException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
