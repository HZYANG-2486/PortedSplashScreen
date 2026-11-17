namespace PortedSplashScreen.Services;

/// <summary>
/// 从文件夹中挑选启动背景图片。挑选结果在插件初始化时确定一次，保证整次启动使用同一张图。
/// </summary>
public static class ImageFolderProvider
{
    /// <summary>
    /// 文件夹中可识别的图片与视频扩展名。视频需要可用的 FFmpeg 才能播放。
    /// </summary>
    public static readonly string[] SupportedExtensions =
    [
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif",
        ".mp4", ".webm", ".mov", ".mkv", ".m4v", ".avi"
    ];

    /// <summary>
    /// 按文件名排序返回文件夹内的图片路径；文件夹不存在或读取失败时返回空列表。
    /// </summary>
    public static IReadOnlyList<string> GetImages(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(folder)
                .Where(file => SupportedExtensions.Contains(
                    Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// 计算本次要使用的图片序号。
    /// </summary>
    /// <param name="count">图片数量。</param>
    /// <param name="mode">0 顺序轮播，1 随机。</param>
    /// <param name="lastIndex">上一次使用的序号，顺序模式下用于切换到下一张。</param>
    /// <param name="randomValue">随机数，仅用于测试。</param>
    /// <returns>图片序号；没有图片时返回 -1。</returns>
    public static int PickIndex(int count, int mode, int lastIndex, int? randomValue = null)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (mode == 1)
        {
            var value = randomValue ?? Random.Shared.Next(count);
            return ((value % count) + count) % count;
        }

        return (Math.Max(-1, lastIndex) + 1) % count;
    }
}
