namespace PortedSplashScreen.Services;

/// <summary>
/// 构造 FFmpeg 解码视频的参数。独立于 Avalonia 与进程逻辑，便于单元测试。
/// </summary>
public static class VideoSplashArguments
{
    /// <summary>
    /// 循环播放、按原始速度读取、缩放裁剪到目标尺寸并输出 BGRA 原始帧。
    /// </summary>
    public static IReadOnlyList<string> Build(string path, int width, int height) =>
    [
        "-hide_banner", "-loglevel", "error", "-nostdin",
        "-stream_loop", "-1", "-re", "-i", path,
        "-an",
        "-vf", $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},fps=30",
        "-f", "rawvideo", "-pix_fmt", "bgra", "pipe:1"
    ];
}
