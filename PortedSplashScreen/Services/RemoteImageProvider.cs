using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace PortedSplashScreen.Services;

/// <summary>
/// 解析自定义启动背景来源：既可以是本地文件，也可以是 http/https 网络图片。
/// 网络图片按 URL 缓存到插件目录，并设置有效期，便于配合每日壁纸类接口使用。
/// </summary>
public static class RemoteImageProvider
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// 网络图片缓存有效期。超过后重新下载，下载失败时仍回退到旧缓存。
    /// </summary>
    public static TimeSpan CacheLifetime { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// 网络图片缓存目录，由插件在初始化时设置。
    /// </summary>
    public static string CacheRoot { get; set; } =
        Path.Combine(Path.GetTempPath(), "PortedSplashScreen", "RemoteImages");

    /// <summary>
    /// 单次网络请求的最长等待时间。超时后若存在旧缓存则回退到旧缓存，避免拖慢启动。
    /// </summary>
    public static TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromSeconds(8);

    private static readonly string[] AllowedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

    /// <summary>
    /// 判断给定字符串是否为 http/https 图片链接。
    /// </summary>
    public static bool IsRemote(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// 把本地路径或网络图片链接解析为可读取的本地文件路径。
    /// 本地文件不存在或网络图片不可用时返回 <see langword="null"/>。
    /// </summary>
    public static async Task<string?> ResolveAsync(string? source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (!IsRemote(source))
        {
            return File.Exists(source) ? source : null;
        }

        var cachePath = GetCachePath(source);
        var hasCache = File.Exists(cachePath);
        if (hasCache && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < CacheLifetime)
        {
            return cachePath;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (DownloadTimeout > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(DownloadTimeout);
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(source, timeoutCts.Token);
            Directory.CreateDirectory(CacheRoot);
            var temp = cachePath + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, timeoutCts.Token);
            File.Move(temp, cachePath, true);
            return cachePath;
        }
        catch (Exception)
        {
            // 每日壁纸接口偶尔不可用时，优先使用旧缓存保证启动画面可显示
            return hasCache ? cachePath : null;
        }
    }

    private static string GetCachePath(string url)
    {
        var uri = new Uri(url);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        return Path.Combine(CacheRoot, hash + GetCacheExtension(uri));
    }

    /// <summary>
    /// 决定缓存文件的扩展名。优先取链接路径中的扩展名；部分接口把格式放在查询参数里
    /// （如 https://…/random?format=mp4），此时从查询参数中寻找已知格式，否则视频会因为
    /// 缓存成 .img 而不被识别为视频。
    /// </summary>
    private static string GetCacheExtension(Uri uri)
    {
        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (AllowedExtensions.Contains(extension) || FfmpegProvider.VideoExtensions.Contains(extension))
        {
            return extension;
        }

        foreach (var candidate in AllowedExtensions.Concat(FfmpegProvider.VideoExtensions))
        {
            if (uri.Query.Contains(candidate[1..], StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return ".img";
    }
}
