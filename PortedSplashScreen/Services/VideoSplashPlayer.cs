using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PortedSplashScreen.Services;

/// <summary>
/// 通过 FFmpeg 把视频解码成 BGRA 原始帧并显示在启动画面的 <see cref="Image"/> 上。
/// 插件不包含 FFmpeg，找不到解码器时创建失败，调用方回退到静态图片。
/// </summary>
public sealed class VideoSplashPlayer : IDisposable
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private readonly Image _target;
    private readonly Process _process;
    private readonly WriteableBitmap _frame;
    private readonly byte[] _pixels;
    private readonly byte[] _renderPixels;
    private readonly LatestFrameBuffer _latest;
    private readonly DispatcherTimer? _timer;
    private readonly int _width;
    private readonly int _height;
    private readonly CancellationTokenSource _cancellation = new();

    private volatile bool _disposed;

    private VideoSplashPlayer(Image target, Process process, int width, int height, bool useRenderThread)
    {
        _target = target;
        _process = process;
        _width = width;
        _height = height;
        _frame = new WriteableBitmap(new PixelSize(width, height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        var frameBytes = width * height * 4;
        _pixels = new byte[frameBytes];
        _renderPixels = new byte[frameBytes];
        _latest = new LatestFrameBuffer(frameBytes);
        // 必须持续清空 stderr，否则 FFmpeg 输出较多时会写满管道缓冲区而卡住解码
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginErrorReadLine();

        // 像素写入与重绘都放在 UI 线程上完成：在后台线程直接写 WriteableBitmap
        // 会与渲染线程竞争，实测会导致画面只更新最初几帧后停止刷新。
        // 实验模式改用后台写入 + 合成器保活动画，尝试绕开启动阶段被占用的 UI 线程。
        if (useRenderThread)
        {
            CompositorKeepAlive.Start(_target);
            _ = Task.Run(() => RenderLoopAsync(_cancellation.Token));
        }
        else
        {
            _timer = new DispatcherTimer { Interval = FrameInterval };
            _timer.Tick += (_, _) => RenderLatestFrame();
            _timer.Start();
        }

        _ = Task.Run(() => ReadLoopAsync(_cancellation.Token));
    }

    private async Task RenderLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                await Task.Delay(FrameInterval, cancellationToken);
                if (!_latest.TryRead(_renderPixels))
                {
                    continue;
                }

                using var locked = _frame.Lock();
                var rowBytes = _width * 4;
                for (var y = 0; y < _height; y++)
                {
                    Marshal.Copy(_renderPixels, y * rowBytes,
                        IntPtr.Add(locked.Address, y * locked.RowBytes), rowBytes);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// 尝试创建播放器；FFmpeg 或视频不可用时返回 <see langword="null"/>。
    /// </summary>
    /// <param name="target">显示帧的控件。</param>
    /// <param name="path">视频文件路径。</param>
    /// <param name="ffmpegPath">FFmpeg 可执行文件路径。</param>
    /// <param name="width">解码输出宽度。</param>
    /// <param name="height">解码输出高度。</param>
    public static VideoSplashPlayer? TryCreate(
        Image target, string path, string ffmpegPath, int width, int height,
        bool useRenderThread = false)
    {
        if (!File.Exists(path) || !File.Exists(ffmpegPath) || width <= 0 || height <= 0)
        {
            return null;
        }

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            foreach (var argument in VideoSplashArguments.Build(path, width, height))
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var player = new VideoSplashPlayer(target, process, width, height, useRenderThread);
            target.Source = player._frame;
            return player;
        }
        catch (Exception)
        {
            // 创建失败时必须回收已经启动的 FFmpeg 进程，否则会残留一个持续解码的进程
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch (Exception)
                {
                    // 进程可能已经退出
                }

                process.Dispose();
            }

            return null;
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = _process.StandardOutput.BaseStream;
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                // 上一帧还没被渲染消费时暂停读取：管道写满后 FFmpeg 会同步阻塞，
                // 这样启动阶段 UI 线程被占用时视频会停在当前帧，恢复后从原处继续而不是跳帧。
                while (!cancellationToken.IsCancellationRequested && !_disposed && _latest.HasPendingFrame)
                {
                    await Task.Delay(10, cancellationToken);
                }

                var read = 0;
                while (read < _pixels.Length)
                {
                    var count = await output.ReadAsync(
                        _pixels.AsMemory(read, _pixels.Length - read), cancellationToken);
                    if (count <= 0)
                    {
                        return;
                    }

                    read += count;
                }

                // 只把帧交给缓冲区，实际的位图写入与重绘由 UI 线程的定时器完成
                _latest.Write(_pixels);
            }
        }
        catch (Exception)
        {
            // 取消、进程退出或管道断开时结束播放
        }
    }

    private void RenderLatestFrame()
    {
        if (_disposed || !_latest.TryRead(_renderPixels))
        {
            return;
        }

        using var locked = _frame.Lock();
        var rowBytes = _width * 4;
        for (var y = 0; y < _height; y++)
        {
            Marshal.Copy(_renderPixels, y * rowBytes,
                IntPtr.Add(locked.Address, y * locked.RowBytes), rowBytes);
        }

        _target.InvalidateVisual();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Stop();
        _cancellation.Cancel();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
            }
        }
        catch (Exception)
        {
            // 进程可能已经退出
        }

        try
        {
            _process.StandardInput.Close();
        }
        catch (Exception)
        {
            // 管道可能已经断开
        }

        if (ReferenceEquals(_target.Source, _frame))
        {
            _target.Source = null;
        }

        _frame.Dispose();
        _process.Dispose();
        _cancellation.Dispose();
    }
}
