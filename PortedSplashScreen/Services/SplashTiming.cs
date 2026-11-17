using System.Diagnostics;

namespace PortedSplashScreen.Services;

/// <summary>
/// 启动画面的固定显示时长控制。
/// </summary>
public static class SplashTiming
{
    private static readonly Stopwatch Stopwatch = new();

    /// <summary>
    /// 记录启动画面开始显示的时间。
    /// </summary>
    public static void MarkStarted()
    {
        Stopwatch.Restart();
    }

    /// <summary>
    /// 启用固定显示时长时，等待剩余时间。启动耗时已经超过固定时长时立即返回。
    /// </summary>
    public static async Task WaitForFixedDurationAsync()
    {
        var settings = Plugin.Instance?.Settings;
        if (settings is not { IsFixedDurationEnabled: true } || !Stopwatch.IsRunning)
        {
            return;
        }

        var remaining = SplashDuration.GetRemaining(
            Stopwatch.Elapsed, true, settings.FixedDurationSeconds);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }
}
