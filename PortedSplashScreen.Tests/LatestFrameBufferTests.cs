using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

public class LatestFrameBufferTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatestFrameBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatestFrameBuffer(-4));
    }

    [Fact]
    public void TryRead_ReturnsFalseBeforeAnyFrameIsWritten()
    {
        var buffer = new LatestFrameBuffer(4);

        Assert.False(buffer.TryRead(new byte[4]));
    }

    [Fact]
    public void TryRead_ReturnsTheWrittenFrameOnce()
    {
        var buffer = new LatestFrameBuffer(4);
        var destination = new byte[4];

        buffer.Write([1, 2, 3, 4]);

        Assert.True(buffer.TryRead(destination));
        Assert.Equal([1, 2, 3, 4], destination);
        // 没有新帧时不应重复渲染同一帧
        Assert.False(buffer.TryRead(destination));
    }

    [Fact]
    public void Write_KeepsOnlyTheLatestFrame()
    {
        var buffer = new LatestFrameBuffer(3);
        var destination = new byte[3];

        buffer.Write([1, 1, 1]);
        buffer.Write([2, 2, 2]);
        buffer.Write([3, 3, 3]);

        Assert.True(buffer.TryRead(destination));
        Assert.Equal([3, 3, 3], destination);
        Assert.False(buffer.TryRead(destination));
    }

    [Fact]
    public void Write_DoesNotCorruptPreviouslyReadFrame()
    {
        var buffer = new LatestFrameBuffer(3);
        var destination = new byte[3];

        buffer.Write([7, 8, 9]);
        Assert.True(buffer.TryRead(destination));

        buffer.Write([4, 5, 6]);

        Assert.Equal([7, 8, 9], destination);
        Assert.True(buffer.TryRead(destination));
        Assert.Equal([4, 5, 6], destination);
    }

    [Fact]
    public void Write_RejectsFrameWithWrongLength()
    {
        var buffer = new LatestFrameBuffer(4);

        Assert.Throws<ArgumentException>(() => buffer.Write([1, 2, 3]));
    }

    [Fact]
    public void TryRead_RejectsTooSmallDestination()
    {
        var buffer = new LatestFrameBuffer(4);
        buffer.Write([1, 2, 3, 4]);

        Assert.Throws<ArgumentException>(() => buffer.TryRead(new byte[3]));
    }

    [Fact]
    public void ConcurrentWriterAndReader_NeverExposesTornFrames()
    {
        const int frameSize = 8;
        var buffer = new LatestFrameBuffer(frameSize);
        var destination = new byte[frameSize];
        var stop = false;

        var writer = Task.Run(() =>
        {
            var value = 1;
            while (!Volatile.Read(ref stop))
            {
                buffer.Write(Enumerable.Repeat(value, frameSize).Select(v => (byte)v).ToArray());
                value = value % 200 + 1;
            }
        });

        for (var i = 0; i < 2000; i++)
        {
            if (buffer.TryRead(destination))
            {
                // 同一帧的所有字节必须一致，说明读取方不会读到写了一半的缓冲区
                Assert.All(destination, b => Assert.Equal(destination[0], b));
            }
        }

        Volatile.Write(ref stop, true);
        writer.Wait(TimeSpan.FromSeconds(5));
    }
}
