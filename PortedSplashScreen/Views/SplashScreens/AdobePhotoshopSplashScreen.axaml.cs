using System;
using System.Windows;
using Avalonia;
using Avalonia.Media.Imaging;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Views;
using PortedSplashScreen.Services;
using ReactiveUI;

namespace PortedSplashScreen.Views.SplashScreens;

/// <summary>
/// AdobePhotoshopSplashScreen.xaml 的交互逻辑
/// </summary>
public partial class AdobePhotoshopSplashScreen : SplashWindowBase
{
    private IDisposable? _splashStatusObserver;
    private AnimatedImagePlayer? _imagePlayer;
    private VideoSplashPlayer? _videoPlayer;
    private bool _assetsReleased;
    public ISplashService SplashService { get; }

    public AdobePhotoshopSplashScreen(ISplashService splashService, Plugin plugin)
    {
        SplashService = splashService;
        InitializeComponent();
        var cornerRadius = plugin.ResolveCornerRadius();
        SplashRootBorder.CornerRadius = new CornerRadius(cornerRadius);
        SplashCornerClip.Attach(SplashRootBorder, cornerRadius);
        // 图片区比外框略小，保持默认 20 → 16 的视觉比例
        var imageRadius = Math.Max(0, cornerRadius - 4);
        SplashImageBorder.CornerRadius = new CornerRadius(imageRadius);
        SplashCornerClip.Attach(SplashImageBorder, imageRadius);
        Closed += (_, _) => ReleaseSplashAssets();
        _ = LoadSplashImageAsync(plugin);
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

    private async Task LoadSplashImageAsync(Plugin plugin)
    {
        // 文件夹图片优先于单张自定义背景
        if (plugin.ResolveFolderSplashImage() is { } folderImage && TryApplySplashImage(folderImage))
        {
            return;
        }

        if (plugin.IsCustomPhotoshopImageAvailable)
        {
            var customPath = await RemoteImageProvider.ResolveAsync(
                plugin.Settings.CustomPhotoshopSplashImagePath);
            if (customPath != null && TryApplySplashImage(customPath))
            {
                return;
            }
        }

        var asset = SplashAssetProvider.PhotoshopAsset;
        var path = SplashAssetProvider.GetCachedPath(asset) ??
                   await SplashAssetProvider.EnsureAsync(asset, plugin.Settings.AllowRemoteAssetDownload);
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
            var ffmpeg = FfmpegProvider.Resolve(Plugin.Instance?.Settings.FfmpegPath);
            if (ffmpeg == null)
            {
                return false;
            }

            // Photoshop 的右侧插图区约为 480×560
            _videoPlayer = VideoSplashPlayer.TryCreate(SplashImageControl, path, ffmpeg, 480, 560,
                Plugin.Instance?.Settings.UseRenderThreadPlayback == true);
            return _videoPlayer != null;
        }

        var player = AnimatedImagePlayer.TryCreate(SplashImageControl, path,
            Plugin.Instance?.Settings.UseRenderThreadPlayback == true);
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

    public override async Task StartSplash()
    {
        _splashStatusObserver ??= SplashService.ObservableForProperty(x => x.SplashStatus)
            .Subscribe(_ => TryRunJobs());
        await base.StartSplash();
        SplashTiming.MarkStarted();
    }

    public override async Task EndSplash()
    {
        _splashStatusObserver?.Dispose();
        _splashStatusObserver = null;
        await SplashTiming.WaitForFixedDurationAsync();
        await base.EndSplash();
    }
}
