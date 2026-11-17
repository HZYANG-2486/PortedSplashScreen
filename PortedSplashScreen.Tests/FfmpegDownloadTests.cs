using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

/// <summary>
/// FFmpeg 下载取消功能测试。
/// 用本机 TCP 服务器模拟慢速下载，验证取消后立即停止并清理临时目录。
/// </summary>
public sealed class FfmpegDownloadTests : IDisposable
{
    private const int TotalBytes = 10 * 1024 * 1024;

    private readonly string _cacheRoot;
    private readonly TcpListener _listener;
    private readonly Task _serverTask;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly string _url;

    static FfmpegDownloadTests()
    {
        // 避免沙箱代理影响 127.0.0.1 的测试请求
        Environment.SetEnvironmentVariable("NO_PROXY", "127.0.0.1,localhost");
        Environment.SetEnvironmentVariable("no_proxy", "127.0.0.1,localhost");
    }

    public FfmpegDownloadTests()
    {
        _cacheRoot = Path.Combine(
            Path.GetTempPath(), "ffmpeg-download-tests", Guid.NewGuid().ToString("N"));
        FfmpegProvider.CacheRoot = _cacheRoot;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _url = $"http://127.0.0.1:{port}/ffmpeg.zip";
        _serverTask = Task.Run(() => ServeSlowDownloadAsync(_serverCts.Token));
    }

    /// <summary>
    /// 先发送响应头，再以 8KB/20ms 的速度持续发送数据，便于在下载中途取消。
    /// </summary>
    private async Task ServeSlowDownloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            var request = new byte[1024];
            await stream.ReadAsync(request, cancellationToken);

            var header = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/octet-stream\r\n" +
                $"Content-Length: {TotalBytes}\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);

            var chunk = new byte[8192];
            for (var i = 0; i < 400; i++)
            {
                await stream.WriteAsync(chunk, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                await Task.Delay(20, cancellationToken);
            }
        }
        catch (Exception)
        {
            // 测试结束时主动关闭连接属于预期情况
        }
    }

    [Fact]
    public async Task DownloadAsync_令牌已取消_不发起下载且不留下临时目录()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                FfmpegProvider.DownloadAsync(null, cts.Token, (_url, ".zip")))
            .WaitAsync(TimeSpan.FromSeconds(10));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3),
            $"预取消的下载应立即结束，实际耗时 {stopwatch.Elapsed}。");
        Assert.False(Directory.Exists(Path.Combine(_cacheRoot, "download")),
            "取消后应清理下载临时目录。");
    }

    [Fact]
    public async Task DownloadAsync_下载中途取消_立即停止并清理临时目录()
    {
        using var cts = new CancellationTokenSource();
        // 收到第一次进度上报（1%）就取消，模拟用户点击“取消”
        var progress = new Progress<double>(_ => cts.Cancel());
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                FfmpegProvider.DownloadAsync(progress, cts.Token, (_url, ".zip")))
            .WaitAsync(TimeSpan.FromSeconds(10));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"下载中途取消应在短时间内结束，实际耗时 {stopwatch.Elapsed}。");
        Assert.False(Directory.Exists(Path.Combine(_cacheRoot, "download")),
            "取消后应清理下载临时目录，不残留半截文件。");
    }

    [Fact]
    public void IsVideoFile_识别视频扩展名且忽略大小写()
    {
        Assert.True(FfmpegProvider.IsVideoFile("splash.MP4"));
        Assert.True(FfmpegProvider.IsVideoFile("/tmp/movie.webm"));
        Assert.True(FfmpegProvider.IsVideoFile("C:\\背景\\clip.mkv"));

        Assert.False(FfmpegProvider.IsVideoFile("splash.png"));
        Assert.False(FfmpegProvider.IsVideoFile(null));
        Assert.False(FfmpegProvider.IsVideoFile(""));
    }

    public void Dispose()
    {
        _serverCts.Cancel();
        _listener.Stop();
        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // 忽略清理异常
        }

        try
        {
            if (Directory.Exists(_cacheRoot))
            {
                Directory.Delete(_cacheRoot, true);
            }
        }
        catch (Exception)
        {
            // 忽略清理异常
        }

        _serverCts.Dispose();
    }
}
