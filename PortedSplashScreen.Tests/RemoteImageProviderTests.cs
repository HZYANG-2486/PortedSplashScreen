using System.Net;
using System.Net.Sockets;
using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

/// <summary>
/// 网络图片解析与缓存机制测试。
/// 使用本机 HttpListener 模拟每日壁纸类接口，避免依赖外部网络。
/// </summary>
public sealed class RemoteImageProviderTests : IDisposable
{
    // 1x1 透明 PNG
    private static readonly byte[] ImageBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string _cacheRoot;
    private readonly HttpListener _listener;
    private readonly string _baseUrl;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;
    private int _requestCount;

    static RemoteImageProviderTests()
    {
        // 避免沙箱代理影响 127.0.0.1 的测试请求
        Environment.SetEnvironmentVariable("NO_PROXY", "127.0.0.1,localhost");
        Environment.SetEnvironmentVariable("no_proxy", "127.0.0.1,localhost");
    }

    public RemoteImageProviderTests()
    {
        _cacheRoot = Path.Combine(
            Path.GetTempPath(), "ported-splash-tests", Guid.NewGuid().ToString("N"));
        RemoteImageProvider.CacheRoot = _cacheRoot;
        RemoteImageProvider.CacheLifetime = TimeSpan.FromHours(6);

        var port = GetFreePort();
        _baseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseUrl);
        _listener.Start();
        _serverTask = Task.Run(ServeAsync);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
            // 测试中可能已手动停止监听
        }

        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 忽略服务器任务退出时的异常
        }

        RemoteImageProvider.CacheLifetime = TimeSpan.FromHours(6);
        try
        {
            Directory.Delete(_cacheRoot, true);
        }
        catch
        {
            // 缓存目录不存在时忽略
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task ServeAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                return;
            }

            Interlocked.Increment(ref _requestCount);
            if (context.Request.Url?.AbsolutePath.Contains("missing", StringComparison.Ordinal) == true)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                continue;
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "image/png";
            context.Response.ContentLength64 = ImageBytes.Length;
            await context.Response.OutputStream.WriteAsync(ImageBytes);
            context.Response.Close();
        }
    }

    private int RequestCount => Volatile.Read(ref _requestCount);

    [Theory]
    [InlineData("http://example.com/a.png", true)]
    [InlineData("https://example.com/a.png", true)]
    [InlineData("HTTP://example.com/a.png", true)]
    [InlineData("ftp://example.com/a.png", false)]
    [InlineData("/tmp/a.png", false)]
    [InlineData("example.com/a.png", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRemote_只接受http与https(string? source, bool expected)
    {
        Assert.Equal(expected, RemoteImageProvider.IsRemote(source));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ResolveAsync_空值返回null(string? source)
    {
        Assert.Null(await RemoteImageProvider.ResolveAsync(source));
    }

    [Fact]
    public async Task ResolveAsync_本地文件存在时返回原路径()
    {
        var localFile = Path.Combine(_cacheRoot, "local.png");
        Directory.CreateDirectory(_cacheRoot);
        await File.WriteAllBytesAsync(localFile, ImageBytes);

        Assert.Equal(localFile, await RemoteImageProvider.ResolveAsync(localFile));
        Assert.Equal(0, RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_本地文件不存在时返回null()
    {
        var missing = Path.Combine(_cacheRoot, "not-exists.png");

        Assert.Null(await RemoteImageProvider.ResolveAsync(missing));
    }

    [Fact]
    public async Task ResolveAsync_下载网络图片并写入缓存()
    {
        var path = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");

        Assert.NotNull(path);
        Assert.EndsWith(".png", path);
        Assert.StartsWith(Path.GetFullPath(_cacheRoot), Path.GetFullPath(path!));
        Assert.True(File.Exists(path));
        Assert.Equal(ImageBytes, await File.ReadAllBytesAsync(path!));
        Assert.Equal(1, RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_缓存有效期内不重复下载()
    {
        var first = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");
        var second = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(1, RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_缓存过期后重新下载()
    {
        var first = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");
        RemoteImageProvider.CacheLifetime = TimeSpan.Zero;
        await Task.Delay(20);

        var second = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");

        Assert.Equal(first, second);
        Assert.Equal(2, RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_下载失败时回退旧缓存()
    {
        var first = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");
        Assert.NotNull(first);

        _listener.Stop();
        await Task.Delay(100);
        RemoteImageProvider.CacheLifetime = TimeSpan.Zero;

        var second = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");

        Assert.Equal(first, second);
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task ResolveAsync_下载失败且无缓存时返回null()
    {
        Assert.Null(await RemoteImageProvider.ResolveAsync(_baseUrl + "missing.png"));
    }

    [Fact]
    public async Task ResolveAsync_链接无扩展名时缓存为img后缀()
    {
        var path = await RemoteImageProvider.ResolveAsync(_baseUrl + "wallpaper");

        Assert.NotNull(path);
        Assert.EndsWith(".img", path);
    }

    [Theory]
    [InlineData("stream?format=mp4", ".mp4")]
    [InlineData("stream?type=video&ext=webm", ".webm")]
    [InlineData("api/random?token=abc&kind=mp4", ".mp4")]
    public async Task ResolveAsync_格式写在查询参数里时保留视频后缀(string relativeUrl, string expectedExtension)
    {
        var path = await RemoteImageProvider.ResolveAsync(_baseUrl + relativeUrl);

        Assert.NotNull(path);
        Assert.EndsWith(expectedExtension, path);
        Assert.True(FfmpegProvider.IsVideoFile(path));
    }

    [Fact]
    public async Task ResolveAsync_不支持的协议按本地路径处理()
    {
        Assert.Null(await RemoteImageProvider.ResolveAsync("ftp://example.com/a.png"));
        Assert.Equal(0, RequestCount);
    }

    [Theory]
    [InlineData("photo.PNG", ".png")]
    [InlineData("photo.jpg", ".jpg")]
    [InlineData("photo.jpeg", ".jpeg")]
    [InlineData("photo.webp", ".webp")]
    [InlineData("photo.bmp", ".bmp")]
    [InlineData("photo.gif", ".gif")]
    [InlineData("photo.txt", ".img")]
    public async Task ResolveAsync_按允许的图片后缀缓存(string fileName, string expectedExtension)
    {
        var path = await RemoteImageProvider.ResolveAsync(_baseUrl + fileName);

        Assert.NotNull(path);
        Assert.EndsWith(expectedExtension, path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ResolveAsync_同一链接生成稳定的缓存文件名()
    {
        var url = _baseUrl + "daily.png";
        var first = await RemoteImageProvider.ResolveAsync(url);

        RemoteImageProvider.CacheLifetime = TimeSpan.Zero;
        await Task.Delay(20);
        var second = await RemoteImageProvider.ResolveAsync(url);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(2, RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_不同查询参数使用不同缓存()
    {
        var first = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png?v=1");
        var second = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png?v=2");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.Equal(2, RequestCount);
    }

    [Fact]
    public async Task ResolveAsync_下载失败时旧缓存内容保持不变()
    {
        var first = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");
        Assert.NotNull(first);

        _listener.Stop();
        await Task.Delay(100);
        RemoteImageProvider.CacheLifetime = TimeSpan.Zero;

        var second = await RemoteImageProvider.ResolveAsync(_baseUrl + "daily.png");

        Assert.Equal(first, second);
        Assert.Equal(ImageBytes, await File.ReadAllBytesAsync(second!));
        Assert.Empty(Directory.GetFiles(_cacheRoot, "*.tmp"));
    }
}
