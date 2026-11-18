namespace PortedSplashScreen.Services;

/// <summary>
/// 单生产者、单消费者的最新帧缓冲。渲染慢于解码时只保留最新一帧，避免帧积压。
/// </summary>
public sealed class LatestFrameBuffer
{
    private readonly byte[] _first;
    private readonly byte[] _second;
    private readonly object _sync = new();

    private byte[] _write;
    private byte[] _read;
    private bool _hasNewFrame;

    public LatestFrameBuffer(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "帧缓冲长度必须大于 0。");
        }

        _first = new byte[length];
        _second = new byte[length];
        _write = _second;
        _read = _first;
    }

    /// <summary>
    /// 单帧字节数。
    /// </summary>
    public int Length => _first.Length;

    /// <summary>
    /// 是否还有尚未被渲染消费的帧。
    /// </summary>
    public bool HasPendingFrame
    {
        get
        {
            lock (_sync)
            {
                return _hasNewFrame;
            }
        }
    }

    /// <summary>
    /// 写入一帧；已有未消费的帧会被直接覆盖。
    /// </summary>
    public void Write(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != Length)
        {
            throw new ArgumentException("帧长度与缓冲区长度不一致。", nameof(frame));
        }

        lock (_sync)
        {
            frame.CopyTo(_write);
            (_write, _read) = (_read, _write);
            _hasNewFrame = true;
        }
    }

    /// <summary>
    /// 取走最新一帧；自上次读取后没有新帧时返回 <see langword="false"/>。
    /// </summary>
    public bool TryRead(Span<byte> destination)
    {
        if (destination.Length < Length)
        {
            throw new ArgumentException("目标缓冲区太小。", nameof(destination));
        }

        lock (_sync)
        {
            if (!_hasNewFrame)
            {
                return false;
            }

            _read.AsSpan(0, Length).CopyTo(destination);
            _hasNewFrame = false;
            return true;
        }
    }
}
