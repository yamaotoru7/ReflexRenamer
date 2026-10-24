namespace FrTools;

/// <summary>シナリオや正解データ取りを、失敗したら最初からやり直す（最大3回）。</summary>
internal static class Retry
{
    public static void Run(string what, Action action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < 3)
            {
                Console.WriteLine($"{what} が失敗したので最初からやり直す（{attempt}回目）: {ex.Message}");
                AppSession.WaitForUserIdle();
            }
        }
    }
}
