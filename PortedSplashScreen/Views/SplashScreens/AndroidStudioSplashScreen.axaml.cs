using Avalonia.Animation.Easings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Views;
using PortedSplashScreen.Services;
using ReactiveUI;

namespace PortedSplashScreen.Views.SplashScreens;

/// <summary>
/// AndroidStudioSplashScreen.xaml 的交互逻辑
/// </summary>
public partial class AndroidStudioSplashScreen : SplashWindowBase
{
    public ISplashService SplashService { get; }
    public Plugin Plugin { get; }
    
    private double _lastProgress = 0.0;
    private IDisposable? _splashStatusObserver;
    private AnimatedImagePlayer? _imagePlayer;
    private VideoSplashPlayer? _videoPlayer;

    private bool _canClose = false;
    private bool _assetsReleased;

    public AndroidStudioSplashScreen(ISplashService splashService, Plugin plugin)
    {
        SplashService = splashService;
        Plugin = plugin;
        InitializeComponent();
        SplashBackgroundBorder.Background = new SolidColorBrush(Plugin.Settings.AndroidStudioBackgroundColor);
        var cornerRadius = Plugin.ResolveCornerRadius();
        SplashBackgroundBorder.CornerRadius = new CornerRadius(cornerRadius);
        SplashCornerClip.Attach(SplashBackgroundBorder, cornerRadius);
        Loaded += OnLoaded;
        Closed += (_, _) => ReleaseSplashAssets();
        _ = LoadSplashImageAsync();
    }

    /// <summary>
    /// 释放动图/视频播放器。真实启动画面关闭时和设置页预览关闭时都会调用。
    /// </summary>
    internal void ReleaseSplashAssets()
    {
        _assetsReleased = true;
        _imagePlayer?.Dispose();
        _imagePlayer = null;
        _videoPlayer?.Dispose();
        _videoPlayer = null;
    }

    /// <summary>
    /// 预览文件夹轮播时切换到指定背景。
    /// </summary>
    internal bool ApplyPreviewImage(string path) => TryApplySplashImage(path);

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var visualProgressBarFill = ElementComposition.GetElementVisual(ProgressBarFill);
        if (visualProgressBarFill != null)
        {
            visualProgressBarFill.Offset = visualProgressBarFill.Offset with { X = -640 };
            visualProgressBarFill.Opacity = 1.0f;
        }
    }

    private Bitmap GetBitmapFromUri(Uri uri)
    {
        using var stream = AssetLoader.Open(uri);
        var bitmap = new Bitmap(stream);
        return bitmap;
    }

    private async Task LoadSplashImageAsync()
    {
        // 文件夹图片优先于单张自定义背景
        if (Plugin.ResolveFolderSplashImage() is { } folderImage && TryApplySplashImage(folderImage))
        {
            return;
        }

        if (Plugin.Settings.AndroidStudioBackgroundSource == 1)
        {
            var customPath = Plugin.Settings.CustomAndroidStudioSplashImagePath;
            var resolvedPath = await RemoteImageProvider.ResolveAsync(customPath);
            try
            {
                if (resolvedPath != null)
                {
                    if (TryApplySplashImage(resolvedPath))
                    {
                        return;
                    }
                }
                else
                {
                    SplashImageControl.Source = GetBitmapFromUri(new Uri(customPath, UriKind.RelativeOrAbsolute));
                    return;
                }
            }
            catch (Exception)
            {
                // 自定义图片不可用时继续尝试内置素材
            }
        }

        var asset = Plugin.ResolveAndroidStudioAsset();
        if (asset == null)
        {
            return;
        }

        var path = SplashAssetProvider.GetCachedPath(asset) ??
                   await SplashAssetProvider.EnsureAsync(asset, Plugin.Settings.AllowRemoteAssetDownload);
        if (path != null)
        {
            TryApplySplashImage(path);
        }
    }

    /// <summary>
    /// 加载静态图片；GIF/动态 WebP 播放动画；视频文件通过 FFmpeg 解码播放。
    /// </summary>
    private bool TryApplySplashImage(string path)
    {
        if (_assetsReleased)
        {
            return false;
        }

        _imagePlayer?.Dispose();
        _imagePlayer = null;
        _videoPlayer?.Dispose();
        _videoPlayer = null;

        if (!File.Exists(path))
        {
            return false;
        }

        if (FfmpegProvider.IsVideoFile(path))
        {
            var ffmpeg = FfmpegProvider.Resolve(Plugin.Settings.FfmpegPath);
            if (ffmpeg == null)
            {
                return false;
            }

            // 窗口为 640×400，解码尺寸与显示区域一致
            _videoPlayer = VideoSplashPlayer.TryCreate(SplashImageControl, path, ffmpeg, 640, 400,
                Plugin.Settings.UseRenderThreadPlayback);
            return _videoPlayer != null;
        }

        var player = AnimatedImagePlayer.TryCreate(SplashImageControl, path,
            Plugin.Settings.UseRenderThreadPlayback);
        if (player != null)
        {
            _imagePlayer = player;
            return true;
        }

        try
        {
            using var stream = File.OpenRead(path);
            SplashImageControl.Source = new Bitmap(stream);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }


    private void SplashServiceOnProgressChanged(object? sender, double e)
    {
        _ = UpdateAnimationAsync(e);

    }

    private async Task UpdateAnimationAsync(double value, bool isFinal = false)
    {
        var visualProgressBarFill = ElementComposition.GetElementVisual(ProgressBarFill);
        if (visualProgressBarFill == null)
        {
            return;
        }
        
        var currentOffset = visualProgressBarFill.Offset;
        var compositor = visualProgressBarFill.Compositor;
        var progressAnimation = compositor.CreateVector3DKeyFrameAnimation();
        progressAnimation.InsertKeyFrame(1.0f, currentOffset with { X = -640 * (1 - value / 100) }, new CubicEaseOut());
        var duration = isFinal ? TimeSpan.FromSeconds(0.15) : TimeSpan.FromSeconds(Math.Max((value - _lastProgress ) / 8, 0.5));
        progressAnimation.Duration = duration;
        visualProgressBarFill.StartAnimation(nameof(visualProgressBarFill.Offset), progressAnimation);
        _lastProgress = value;

        await Task.Delay(duration);
    }
    

    private void AndroidStudioSplashScreen_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_canClose)
        {
            e.Cancel = true;
        }
    }
    
    public override async Task StartSplash()
    {
        SplashService.ProgressChanged += SplashServiceOnProgressChanged;
        _splashStatusObserver ??= SplashService.ObservableForProperty(x => x.SplashStatus)
            .Subscribe(_ => TryRunJobs());
        await base.StartSplash();
        SplashTiming.MarkStarted();
    }

    public override async Task EndSplash()
    {
        SplashService.ProgressChanged -= SplashServiceOnProgressChanged;
        _splashStatusObserver?.Dispose();
        _splashStatusObserver = null;
        await UpdateAnimationAsync(100, true);
        await SplashTiming.WaitForFixedDurationAsync();
        _canClose = true;
        await base.EndSplash();
    }
}