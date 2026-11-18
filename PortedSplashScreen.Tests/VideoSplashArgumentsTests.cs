using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

public class VideoSplashArgumentsTests
{
    private static IReadOnlyList<string> Build(string path = "/tmp/clip.mp4", int width = 480, int height = 560)
        => VideoSplashArguments.Build(path, width, height);

    [Fact]
    public void Build_EnablesInfiniteLoopAndRealtimePacing()
    {
        var arguments = Build();

        var loopIndex = arguments.ToList().IndexOf("-stream_loop");
        Assert.True(loopIndex >= 0);
        Assert.Equal("-1", arguments[loopIndex + 1]);
        Assert.Contains("-re", arguments);
    }

    [Fact]
    public void Build_ScalesAndCropsToTargetSizeAt30Fps()
    {
        var arguments = Build(width: 960, height: 540);

        var filterIndex = arguments.ToList().IndexOf("-vf");
        Assert.True(filterIndex >= 0);
        var filter = arguments[filterIndex + 1];
        Assert.Contains("scale=960:540:force_original_aspect_ratio=increase", filter);
        Assert.Contains("crop=960:540", filter);
        Assert.Contains("fps=30", filter);
    }

    [Fact]
    public void Build_OutputsBgraRawFramesOnStdout()
    {
        var arguments = Build();

        var formatIndex = arguments.ToList().IndexOf("-f");
        Assert.Equal("rawvideo", arguments[formatIndex + 1]);
        var pixelFormatIndex = arguments.ToList().IndexOf("-pix_fmt");
        Assert.Equal("bgra", arguments[pixelFormatIndex + 1]);
        Assert.Equal("pipe:1", arguments[^1]);
        Assert.Contains("-an", arguments);
        Assert.Contains("-nostdin", arguments);
    }

    [Fact]
    public void Build_KeepsPathWithSpacesAsSingleArgument()
    {
        const string path = "/tmp/my splash video.mp4";

        var arguments = Build(path);

        var inputIndex = arguments.ToList().IndexOf("-i");
        Assert.Equal(path, arguments[inputIndex + 1]);
        Assert.Contains(path, arguments);
    }

    [Fact]
    public void Build_DoesNotAlterOrderOfLoopBeforeInput()
    {
        var arguments = Build().ToList();

        Assert.True(arguments.IndexOf("-stream_loop") < arguments.IndexOf("-i"));
        Assert.True(arguments.IndexOf("-re") < arguments.IndexOf("-i"));
    }
}
