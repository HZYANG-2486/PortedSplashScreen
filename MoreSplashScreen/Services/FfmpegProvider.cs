using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace PortedSplashScreen.Services;

/// <summary>
/// 定位或下载 FFmpeg。插件本身不附带解码器，视频背景必须依赖用户安装或下载的 FFmpeg。
/// </summary>
public static class FfmpegProvider
{
    /// <summary>
    /// 插件不包含解码器的说明文本，设置页与文档共用。
    /// </summary>
    public const string NotBundledNotice =
        "插件不包含 FFmpeg 解码器。使用 mp4/webm/mov/mkv 等视频作为启动背景时，需要安装 FFmpeg，或在此处下载到插件配置目录。";

    /// <summary>
    /// 下载与解压后的缓存目录，由插件在初始化时指定到配置目录。
    /// </summary>
    public static string? CacheRoot { get; set; }

    /// <summary>
    /// 可识别的视频扩展名。
    /// </summary>
    public static readonly string[] VideoExtensions =
        [".mp4", ".webm", ".mov", ".mkv", ".m4v", ".avi"];

    /// <summary>FFmpeg 可执行文件名。</summary>
    public static string ExecutableName => OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    /// <summary>该文件是否是当前平台可尝试播放的视频。</summary>
    public static bool IsVideoFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 查找可用的 FFmpeg：自定义路径 → 插件缓存目录 → 系统 PATH。
    /// </summary>
    /// <param name="customPath">设置中指定的文件或目录。</param>
    public static string? Resolve(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                return customPath;
            }

            if (Directory.Exists(customPath))
            {
                var candidate = Path.Combine(customPath, ExecutableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(CacheRoot))
        {
            var cached = Path.Combine(CacheRoot, ExecutableName);
            if (File.Exists(cached))
            {
                return cached;
            }
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "")
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), ExecutableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // PATH 中存在非法字符时忽略该条目
            }
        }

        return null;
    }

    /// <summary>
    /// 当前平台可用的下载源；不支持时返回 <see langword="null"/>。
    /// </summary>
    public static (string Url, string ArchiveExtension)? GetDownloadSource()
    {
        if (OperatingSystem.IsWindows())
        {
            return ("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip", ".zip");
        }

        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => ("https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-arm64-static.tar.xz", ".tar.xz"),
                Architecture.X64 => ("https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz", ".tar.xz"),
                _ => null
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            // evermeet.cx 提供通用 x86_64 静态构建，Apple Silicon 需要 Rosetta
            return ("https://evermeet.cx/ffmpeg/getrelease/zip", ".zip");
        }

        return null;
    }

    /// <summary>
    /// 下载并解压 FFmpeg 到插件缓存目录，返回可执行文件路径。
    /// </summary>
    /// <param name="progress">下载进度（0-100）；无法获取总大小时为 0。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="downloadSource">测试或自定义下载源；为空时使用 <see cref="GetDownloadSource"/>。</param>
    public static async Task<string?> DownloadAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        (string Url, string ArchiveExtension)? downloadSource = null)
    {
        var source = downloadSource ?? GetDownloadSource();
        if (source == null)
        {
            return null;
        }

        var root = CacheRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PortedSplashScreen", "Ffmpeg");
        }

        var workDirectory = Path.Combine(root, "download");
        Directory.CreateDirectory(root);
        if (Directory.Exists(workDirectory))
        {
            Directory.Delete(workDirectory, true);
        }

        Directory.CreateDirectory(workDirectory);
        var archivePath = Path.Combine(workDirectory, "ffmpeg" + source.Value.ArchiveExtension);

        try
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var response = await http.GetAsync(
                       source.Value.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength;

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(archivePath);
                var buffer = new byte[81920];
                long received = 0;
                var lastReported = -1d;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    if (total > 0)
                    {
                        // 只在百分比变化时上报，避免每个缓冲区都触发一次 UI 更新
                        var percent = received * 100d / total.Value;
                        if (percent - lastReported >= 1 || percent >= 100)
                        {
                            lastReported = percent;
                            progress?.Report(percent);
                        }
                    }
                }
            }

            progress?.Report(100);
            var extracted = await ExtractAsync(archivePath, workDirectory, source.Value.ArchiveExtension, cancellationToken);
            if (extracted == null)
            {
                return null;
            }

            var target = Path.Combine(root, ExecutableName);
            File.Copy(extracted, target, true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(target,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            return target;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDirectory))
                {
                    Directory.Delete(workDirectory, true);
                }
            }
            catch (Exception)
            {
                // 清理失败不影响使用
            }
        }
    }

    private static async Task<string?> ExtractAsync(
        string archivePath, string destination, string extension, CancellationToken cancellationToken)
    {
        if (extension == ".zip")
        {
            // 解压可能耗时较长，放到线程池避免阻塞界面
            await Task.Run(
                () => ZipFile.ExtractToDirectory(archivePath, destination, true),
                cancellationToken);
        }
        else
        {
            // .NET 不支持 xz 解压，交给系统 tar
            var startInfo = new ProcessStartInfo
            {
                FileName = "tar",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-xf");
            startInfo.ArgumentList.Add(archivePath);
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(destination);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                return null;
            }
        }

        return Directory.EnumerateFiles(destination, ExecutableName, SearchOption.AllDirectories)
            .FirstOrDefault();
    }
}
