using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

namespace PortedSplashScreen.Services;

/// <summary>
/// 在启动画面的 <see cref="Image"/> 上播放 GIF / 动态 WebP。
/// 逐帧解码并复用同一张位图，避免一次性缓存所有帧占用大量内存。
/// </summary>
public sealed class AnimatedImagePlayer : IDisposable
{
    private readonly Image _target;
    private readonly SKCodec _codec;
    private readonly SKBitmap _buffer;
    private readonly WriteableBitmap _frame;
    private readonly DispatcherTimer _timer;
    private readonly byte[] _row;

    private int _index;
    private bool _disposed;

    private AnimatedImagePlayer(Image target, SKCodec codec)
    {
        _target = target;
        _codec = codec;
        _buffer = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height,
            SKColorType.Bgra8888, SKAlphaType.Premul));
        _frame = new WriteableBitmap(new PixelSize(codec.Info.Width, codec.Info.Height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _row = new byte[codec.Info.Width * 4];
        _timer = new DispatcherTimer();
        _timer.Tick += OnTick;
    }

    /// <summary>
    /// 尝试从文件创建播放器；静态图片、无法解码或只有一帧时返回 <see langword="null"/>。
    /// </summary>
    public static AnimatedImagePlayer? TryCreate(Image target, string path)
    {
        SKCodec? codec = null;
        AnimatedImagePlayer? player = null;
        try
        {
            codec = SKCodec.Create(path);
            if (codec == null || codec.FrameCount <= 1)
            {
                codec?.Dispose();
                return null;
            }

            player = new AnimatedImagePlayer(target, codec);
            if (!player.ShowFrame(0))
            {
                player.Dispose();
                player = null;
                codec = null;
                return null;
            }

            target.Source = player._frame;
            player._timer.Interval = player.GetDelay(0);
            player._timer.Start();
            return player;
        }
        catch (Exception)
        {
            // 构造或首帧解码失败时回收已分配的原生资源
            if (player != null)
            {
                player.Dispose();
            }
            else
            {
                codec?.Dispose();
            }

            return null;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _index = AnimatedImageTiming.GetNextFrameIndex(_index, _codec.FrameCount);
        if (ShowFrame(_index))
        {
            _target.InvalidateVisual();
        }

        _timer.Interval = GetDelay(_index);
    }

    private TimeSpan GetDelay(int index)
    {
        int? duration = index >= 0 && index < _codec.FrameInfo.Length
            ? _codec.FrameInfo[index].Duration
            : null;
        return AnimatedImageTiming.GetFrameDelay(duration);
    }

    private bool ShowFrame(int index)
    {
        if (_disposed)
        {
            return false;
        }

        try
        {
            if (index == 0)
            {
                // 循环回到第一帧时清空上一轮遗留的像素
                _buffer.Erase(SKColors.Transparent);
            }

            var options = new SKCodecOptions(index, index - 1);
            var result = _codec.GetPixels(_buffer.Info, _buffer.GetPixels(), options);
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                return false;
            }

            using var locked = _frame.Lock();
            for (var y = 0; y < _buffer.Info.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(_buffer.GetPixels(), y * _buffer.RowBytes), _row, 0, _row.Length);
                Marshal.Copy(_row, 0, IntPtr.Add(locked.Address, y * locked.RowBytes),
                    Math.Min(_row.Length, locked.RowBytes));
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _codec.Dispose();
        _buffer.Dispose();
        _frame.Dispose();
    }
}
