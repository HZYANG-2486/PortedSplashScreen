using System.Net.Http;
using System.Security.Cryptography;

namespace PortedSplashScreen.Services;

/// <summary>
/// 一张内置启动背景素材。素材不随插件分发，而是按需从原版仓库下载并校验。
/// </summary>
/// <param name="Name">素材名，同时也是文件名（不含扩展名）。</param>
/// <param name="RelativePath">素材在原版仓库 Assets 目录下的相对路径。</param>
/// <param name="Sha256">原版仓库中该文件的 SHA-256，用于校验下载结果。</param>
public sealed record SplashAsset(string Name, string RelativePath, string Sha256)
{
    public string FileName => Name + Path.GetExtension(RelativePath);
}

/// <summary>
/// 负责按需下载、校验并缓存内置启动背景素材。
/// </summary>
public static class SplashAssetProvider
{
    private const string RawBase =
        "https://raw.githubusercontent.com/HelloWRC/MoreSplashScreen/master/MoreSplashScreen/Assets/";

    private const string JsDelivrBase =
        "https://cdn.jsdelivr.net/gh/HelloWRC/MoreSplashScreen@master/MoreSplashScreen/Assets/";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    /// <summary>
    /// 素材缓存目录，由插件在初始化时设置为自己的配置目录。
    /// </summary>
    public static string CacheRoot { get; set; } =
        Path.Combine(Path.GetTempPath(), "MoreSplashScreen", "AssetsCache");

    /// <summary>
    /// 一次素材获取（包含等待锁与两个下载源）的最长等待时间，避免拖慢启动。
    /// </summary>
    public static TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromSeconds(8);

    public static IReadOnlyList<SplashAsset> AndroidStudioAssets { get; } =
    [
        new("Griseo", "AndroidStudio/Griseo.webp", "9683602c0f6aede5882144be4a7ee26a55dca62ba06da69ed4e646757b933bc6"),
        new("RyouYamada", "AndroidStudio/RyouYamada.webp", "f661cb9571e444d1e47584bc9efcba4771d7b6ac4a331d443feb813e645ee96f"),
        new("Khaslana", "AndroidStudio/Khaslana.webp", "00bc854449ef7eae5c33d13a718d900f677a72adc3edef3317238199687ce699"),
        new("Misha", "AndroidStudio/Misha.webp", "4a7d0d0ceeeba33c9f52052a13ebd003e252384d590ad5ed5c0b199c6eb1ee83"),
    ];

    public static SplashAsset PhotoshopAsset { get; } =
        new("Foremist", "AdobePhotoshop/Foremist.webp", "386abbe8717b54c0bbb553ac648c4c1cc0bbcdaa3abcc6a6cec34044afae25f3");

    public static SplashAsset? FindAndroidStudioAsset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return AndroidStudioAssets.FirstOrDefault(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetCachePath(SplashAsset asset) => Path.Combine(CacheRoot, asset.FileName);

    /// <summary>
    /// 返回已缓存且校验通过的素材路径，未缓存或校验失败时返回 <see langword="null"/>。
    /// </summary>
    public static string? GetCachedPath(SplashAsset asset)
    {
        var path = GetCachePath(asset);
        return File.Exists(path) && VerifyHash(path, asset.Sha256) ? path : null;
    }

    /// <summary>
    /// 确保素材可用：优先使用缓存，否则在允许的情况下从原版仓库下载（GitHub raw，失败后回退 jsDelivr）。
    /// </summary>
    /// <returns>可用的素材路径；不可用时返回 <see langword="null"/>。</returns>
    public static async Task<string?> EnsureAsync(SplashAsset asset, bool allowDownload,
        CancellationToken cancellationToken = default)
    {
        var cached = GetCachedPath(asset);
        if (cached != null)
        {
            return cached;
        }

        if (!allowDownload)
        {
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (DownloadTimeout > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(DownloadTimeout);
        }

        try
        {
            await DownloadLock.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        try
        {
            // 等待期间可能已被其它调用方下载完成
            cached = GetCachedPath(asset);
            if (cached != null)
            {
                return cached;
            }

            foreach (var baseUrl in new[] { RawBase, JsDelivrBase })
            {
                try
                {
                    var bytes = await Http.GetByteArrayAsync(baseUrl + asset.RelativePath, timeoutCts.Token);
                    if (!string.Equals(ComputeHash(bytes), asset.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(CacheRoot);
                    var target = GetCachePath(asset);
                    var temp = target + ".tmp";
                    await File.WriteAllBytesAsync(temp, bytes, timeoutCts.Token);
                    File.Move(temp, target, true);
                    return target;
                }
                catch (Exception)
                {
                    // 尝试下一个下载源
                }
            }

            return null;
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    /// <summary>
    /// 检查全部内置素材的缓存状态，返回可用数量与总数。
    /// </summary>
    public static (int Ready, int Total) GetCacheStatus()
    {
        var assets = AndroidStudioAssets.Append(PhotoshopAsset).ToList();
        return (assets.Count(a => GetCachedPath(a) != null), assets.Count);
    }

    private static bool VerifyHash(string path, string expected)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return string.Equals(ComputeHash(stream), expected, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string ComputeHash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string ComputeHash(Stream stream) =>
        Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}
