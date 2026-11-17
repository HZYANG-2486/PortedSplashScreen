namespace PortedSplashScreen.Services;

/// <summary>
/// 启动画面固定显示时长的计算。与界面无关，便于单元测试。
/// </summary>
public static class SplashDuration
{
    /// <summary>允许设置的最长固定显示时长（秒）。</summary>
    public const double MaxSeconds = 300;

    /// <summary>
    /// 把设置中的秒数限制在允许范围内，非法值回退为 0。
    /// </summary>
    public static double NormalizeSeconds(double seconds)
        => double.IsFinite(seconds) ? Math.Clamp(seconds, 0, MaxSeconds) : 0;

    /// <summary>
    /// 计算固定显示的目标时长；未启用时为零。
    /// </summary>
    public static TimeSpan GetTarget(bool enabled, double seconds)
        => enabled ? TimeSpan.FromSeconds(NormalizeSeconds(seconds)) : TimeSpan.Zero;

    /// <summary>
    /// 计算还需要等待的时间：未启用或已经超过目标时长时返回零。
    /// </summary>
    /// <param name="elapsed">启动画面已经显示的时长。</param>
    /// <param name="enabled">是否启用固定显示时长。</param>
    /// <param name="seconds">设置的固定显示时长（秒）。</param>
    public static TimeSpan GetRemaining(TimeSpan elapsed, bool enabled, double seconds)
    {
        if (!enabled)
        {
            return TimeSpan.Zero;
        }

        var remaining = GetTarget(true, seconds) - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
