namespace PortedSplashScreen.Services;

/// <summary>
/// 动图播放的帧延时与帧序计算。与界面无关，便于单元测试。
/// </summary>
public static class AnimatedImageTiming
{
    /// <summary>单帧最短显示时间，避免损坏文件中的 0 延时导致空转。</summary>
    public const int MinFrameDelayMilliseconds = 20;

    /// <summary>文件没有提供帧延时时使用的默认值。</summary>
    public const int DefaultFrameDelayMilliseconds = 100;

    /// <summary>
    /// 把文件中的帧延时换算为实际使用的间隔，过小或缺失时回退到默认值。
    /// </summary>
    /// <param name="frameDurationMilliseconds">文件记录的帧延时（毫秒），缺失时为 <see langword="null"/>。</param>
    public static TimeSpan GetFrameDelay(int? frameDurationMilliseconds)
    {
        var duration = frameDurationMilliseconds ?? DefaultFrameDelayMilliseconds;
        return TimeSpan.FromMilliseconds(Math.Max(MinFrameDelayMilliseconds, duration));
    }

    /// <summary>
    /// 计算下一帧的序号，到达末帧后回到第一帧。
    /// </summary>
    /// <param name="currentIndex">当前帧序号。</param>
    /// <param name="frameCount">总帧数。</param>
    public static int GetNextFrameIndex(int currentIndex, int frameCount)
    {
        if (frameCount <= 0 || currentIndex < 0)
        {
            return 0;
        }

        return (currentIndex + 1) % frameCount;
    }
}
