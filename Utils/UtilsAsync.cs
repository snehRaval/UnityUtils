using System;
using System.Threading.Tasks;

public static class UtilsAsync
{
    /// <summary>
    /// Async delay with Task.Delay.
    /// Usage: await UtilsAsync.Delay(2000, () => Debug.Log("Done after 2s"));
    /// </summary>
    public static async Task Delay(int milliseconds, Action callback)
    {
        await Task.Delay(milliseconds);
        callback?.Invoke();
    }
}