
using System.IO;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Views;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PortedSplashScreen.Models;
using PortedSplashScreen.Services;
using PortedSplashScreen.Views.SettingsPages;
using PortedSplashScreen.Views.SplashScreens;

namespace PortedSplashScreen;

[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>
    /// 当前插件实例，供启动屏幕读取全局设置。
    /// </summary>
    public static Plugin? Instance { get; private set; }

    public Settings Settings { get; set; } = new();

    /// <summary>
    /// 本次启动从图片文件夹中选中的背景图片；未启用或文件夹内没有图片时为 <see langword="null"/>。
    /// </summary>
    public string? FolderSplashImagePath { get; private set; }

    /// <summary>
    /// 获取当前实际生效的启动屏幕圆角大小。
    /// </summary>
    public double ResolveCornerRadius() =>
        Settings.CornerRadius >= 0
            ? Settings.CornerRadius
            : SplashStyleDefaults.GetCornerRadius(Settings.SplashKind);

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Instance = this;
        Settings = ConfigureFileHelper.LoadConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"));  // 加载配置文件
        Settings.PropertyChanged += (sender, args) =>
        {
            ConfigureFileHelper.SaveConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"), Settings);  // 保存配置文件
            if (args.PropertyName == nameof(Settings.NetworkTimeoutSeconds))
            {
                ApplyNetworkTimeout();
            }
        };

        // 内置背景素材不随插件分发，缓存到插件配置目录
        SplashAssetProvider.CacheRoot = Path.Combine(PluginConfigFolder, "AssetsCache");
        RemoteImageProvider.CacheRoot = Path.Combine(PluginConfigFolder, "RemoteImages");
        FfmpegProvider.CacheRoot = Path.Combine(PluginConfigFolder, "Ffmpeg");
        MigrateLegacyAssetPath();
        MigrateBackgroundSources();
        ApplyNetworkTimeout();
        SelectFolderSplashImage();
        _ = PrefetchSplashAssetsAsync();

        services.AddSettingsPage<SplashSettingsPage>();

        // 异常崩溃、内存看门狗等场景会以 -q 静默重启，此时可以完全不注册启动画面。
        if (QuietRestartPolicy.ShouldSkipSplash(
                Settings.SkipSplashOnQuietRestart,
                Environment.GetCommandLineArgs()))
        {
            return;
        }

        switch (Settings.SplashKind)
        {
            case 1:  // Android Studio
                services.AddTransient<ISplashProvider, AndroidStudioSplashScreen>();
                break;
            case 2:  // Microsoft 365
                services.AddTransient<ISplashProvider, Microsoft365SplashScreen>();
                break;
            case 3:  // Microsoft Office (2013-2021)
                services.AddTransient<ISplashProvider, OfficeLegacySplashScreen>();
                break;
            case 6:  // IslandOnTop
                services.AddTransient<ISplashProvider, IslandSplashScreen>();
                break;
            case 7:  // Adobe Photoshop
                services.AddTransient<ISplashProvider, AdobePhotoshopSplashScreen>();
                break;
            case 8:  // ALLS
                services.AddTransient<ISplashProvider, AllsSplashScreen>();
                break;
        }
    }

    /// <summary>
    /// 解析 Android Studio 启动画面当前应使用的内置背景素材，用户指定了本地文件时返回 <see langword="null"/>。
    /// </summary>
    public SplashAsset? ResolveAndroidStudioAsset()
    {
        // 自定义文件模式下不显示内置素材
        if (Settings.AndroidStudioBackgroundSource == 1)
        {
            return null;
        }

        // 内置模式下允许从内置素材中选择一张
        if (Settings.AndroidStudioBackgroundSource == 0 &&
            !string.IsNullOrWhiteSpace(Settings.CustomAndroidStudioSplashImagePath))
        {
            var selected = SplashAssetProvider.FindAndroidStudioAsset(
                Path.GetFileNameWithoutExtension(Settings.CustomAndroidStudioSplashImagePath));
            if (selected != null)
            {
                return selected;
            }
        }

        // 当前 ClassIsland 版本的代号没有对应插画时，退回第一张内置素材，避免新配置下没有背景
        return SplashAssetProvider.FindAndroidStudioAsset(AppBase.AppCodeName)
               ?? SplashAssetProvider.AndroidStudioAssets.FirstOrDefault();
    }

    /// <summary>
    /// Adobe Photoshop 启动屏幕是否已指定可用的自定义背景文件（图片或视频）。
    /// </summary>
    public bool IsCustomPhotoshopImageAvailable =>
        Settings.PhotoshopBackgroundSource == 1 &&
        (RemoteImageProvider.IsRemote(Settings.CustomPhotoshopSplashImagePath) ||
         (!string.IsNullOrWhiteSpace(Settings.CustomPhotoshopSplashImagePath) &&
          File.Exists(Settings.CustomPhotoshopSplashImagePath)));

    /// <summary>
    /// 当前样式的背景来源是否为文件夹轮播。
    /// </summary>
    public bool IsFolderBackgroundActive => Settings.SplashKind switch
    {
        1 => Settings.AndroidStudioBackgroundSource == 2,
        7 => Settings.PhotoshopBackgroundSource == 2,
        _ => false
    };

    /// <summary>
    /// 兼容旧版配置：把原来的“启用自定义背景/文件夹”开关迁移为背景来源选项。
    /// </summary>
    private void MigrateBackgroundSources()
    {
        if (Settings.IsImageFolderEnabled)
        {
            if (Settings.SplashKind == 1 &&
                Settings.AndroidStudioBackgroundSource == 0)
            {
                Settings.AndroidStudioBackgroundSource = 2;
            }
            else if (Settings.SplashKind == 7 &&
                     Settings.PhotoshopBackgroundSource == 0)
            {
                Settings.PhotoshopBackgroundSource = 2;
            }
        }
        else if (Settings.SplashKind == 1 &&
                 Settings.IsCustomAndroidStudioSplashImageEnabled &&
                 Settings.AndroidStudioBackgroundSource == 0)
        {
            Settings.AndroidStudioBackgroundSource = 1;
        }
        else if (Settings.SplashKind == 7 &&
                 Settings.IsCustomPhotoshopSplashImageEnabled &&
                 Settings.PhotoshopBackgroundSource == 0)
        {
            Settings.PhotoshopBackgroundSource = 1;
        }

        // 旧开关已完成迁移，避免以后切换模式时又被旧值覆盖
        Settings.IsImageFolderEnabled = false;
        Settings.IsCustomAndroidStudioSplashImageEnabled = false;
        Settings.IsCustomPhotoshopSplashImageEnabled = false;
    }

    /// <summary>
    /// 兼容旧版配置：内置背景在旧版本中保存的是插件内 avares:// 资源路径，这里迁移为素材名。
    /// </summary>
    private void MigrateLegacyAssetPath()
    {
        const string legacyPrefix = "avares://MoreSplashScreen/Assets/AndroidStudio/";
        if (Settings.CustomAndroidStudioSplashImagePath.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            Settings.CustomAndroidStudioSplashImagePath =
                Path.GetFileNameWithoutExtension(Settings.CustomAndroidStudioSplashImagePath);
        }
    }

    /// <summary>
    /// 本次启动实际生效的文件夹背景图片。
    /// </summary>
    public string? ResolveFolderSplashImage() =>
        IsFolderBackgroundActive ? FolderSplashImagePath : null;

    /// <summary>
    /// 把设置中的网络超时应用到图片与素材下载。
    /// </summary>
    private void ApplyNetworkTimeout()
    {
        var timeout = TimeSpan.FromSeconds(Math.Clamp(Settings.NetworkTimeoutSeconds, 1, 120));
        RemoteImageProvider.DownloadTimeout = timeout;
        SplashAssetProvider.DownloadTimeout = timeout;
    }

    /// <summary>
    /// 在插件初始化时从图片文件夹中挑选本次启动要使用的图片。
    /// </summary>
    private void SelectFolderSplashImage()
    {
        FolderSplashImagePath = null;
        var images = ImageFolderProvider.GetImages(Settings.CustomImageFolderPath);
        var index = ImageFolderProvider.PickIndex(
            images.Count, Settings.ImageFolderMode, Settings.ImageFolderIndex);
        if (index < 0)
        {
            return;
        }

        Settings.ImageFolderIndex = index;
        FolderSplashImagePath = images[index];
    }

    private async Task PrefetchSplashAssetsAsync()
    {
        try
        {
            IEnumerable<SplashAsset?> assets = Settings.SplashKind switch
            {
                1 => [ResolveAndroidStudioAsset()],
                7 when Settings.PhotoshopBackgroundSource == 0 => [SplashAssetProvider.PhotoshopAsset],
                _ => []
            };

            foreach (var asset in assets.OfType<SplashAsset>())
            {
                await SplashAssetProvider.EnsureAsync(asset, Settings.AllowRemoteAssetDownload);
            }
        }
        catch (Exception)
        {
            // 预取失败不影响启动，画面会自动退回占位背景
        }
    }
}
