namespace PortedSplashScreen.Services;

/// <summary>
/// 预览文件夹轮播时的序号推进。真实启动仍然是每次启动选一张，这里只服务于设置页预览。
/// </summary>
public static class CarouselStepper
{
    /// <summary>
    /// 按偏移量推进序号并处理首尾循环；没有图片时返回 -1。
    /// </summary>
    /// <param name="current">当前序号。</param>
    /// <param name="count">图片数量。</param>
    /// <param name="delta">偏移量，1 为下一张，-1 为上一张。</param>
    public static int Move(int current, int count, int delta)
    {
        if (count <= 0)
        {
            return -1;
        }

        return (((current + delta) % count) + count) % count;
    }
}
