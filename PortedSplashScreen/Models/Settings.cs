using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PortedSplashScreen.Models;

public partial class Settings : ObservableObject
{
    /// <summary>
    /// 启动动画类型
    /// </summary>
    /// <value>
    /// 0 - 应用默认 <br/>
    /// 1 - Android Studio <br/>
    /// 2 - Microsoft 365 <br/>
    /// 3 - Microsoft Office (2013-2021)<br/>
    /// 4 - JetBrains IDEs <br/>
    /// 5 - dotUltimate <br/>
    /// 6 - IslandOnTop <br/>
    /// 7 - Adobe Photoshop <br/>
    /// </value>
    [ObservableProperty] private int _splashKind = 0;

    [ObservableProperty] private bool _isCustomAndroidStudioSplashImageEnabled = false;

    [ObservableProperty] private string _customAndroidStudioSplashImagePath = "";

    [ObservableProperty] private int _customAndroidStudioSplashImageKind = 0;

    /// <summary>
    /// Android Studio 启动背景来源：0 内置，1 自定义文件（图片或视频），2 文件夹轮播。
    /// </summary>
    [ObservableProperty] private int _androidStudioBackgroundSource = 0;

    /// <summary>
    /// Android Studio 启动屏幕人物插画背后的底色。
    /// </summary>
    [ObservableProperty] private Color _androidStudioBackgroundColor = Colors.White;

    /// <summary>
    /// 启动屏幕的圆角大小；-1 表示跟随当前样式在原仓库中的默认值。
    /// </summary>
    [ObservableProperty] private double _cornerRadius = -1;

    [ObservableProperty] private bool _isCustomPhotoshopSplashImageEnabled = false;

    [ObservableProperty] private string _customPhotoshopSplashImagePath = "";

    /// <summary>
    /// Adobe Photoshop 启动背景来源：0 内置，1 自定义文件（图片或视频），2 文件夹轮播。
    /// </summary>
    [ObservableProperty] private int _photoshopBackgroundSource = 0;

    /// <summary>
    /// 是否允许在需要时从原版仓库下载内置启动背景素材。
    /// </summary>
    [ObservableProperty] private bool _allowRemoteAssetDownload = true;

    /// <summary>
    /// 网络图片与内置素材下载的最长等待时间（秒），超时后使用缓存或占位背景。
    /// </summary>
    [ObservableProperty] private double _networkTimeoutSeconds = 8;

    /// <summary>
    /// 是否从文件夹中挑选启动背景图片。
    /// </summary>
    [ObservableProperty] private bool _isImageFolderEnabled = false;

    /// <summary>
    /// 存放启动背景图片的文件夹。
    /// </summary>
    [ObservableProperty] private string _customImageFolderPath = "";

    /// <summary>
    /// 文件夹图片的切换方式：0 顺序轮播，1 随机。
    /// </summary>
    [ObservableProperty] private int _imageFolderMode = 0;

    /// <summary>
    /// 顺序轮播时上一次使用的图片序号，用于下次启动切换到下一张。
    /// </summary>
    [ObservableProperty] private int _imageFolderIndex = -1;

    /// <summary>
    /// 是否启用固定显示时长。
    /// </summary>
    [ObservableProperty] private bool _isFixedDurationEnabled = false;

    /// <summary>
    /// 启动画面至少显示的时长（秒）。
    /// </summary>
    [ObservableProperty] private double _fixedDurationSeconds = 5;

    /// <summary>
    /// 因异常等原因静默重启时是否跳过启动画面，避免打扰。
    /// </summary>
    [ObservableProperty] private bool _skipSplashOnQuietRestart = true;

    /// <summary>
    /// 自定义 FFmpeg 可执行文件或所在目录；留空时自动查找插件缓存与系统 PATH。
    /// </summary>
    [ObservableProperty] private string _ffmpegPath = "";
}

/// <summary>
/// 各启动屏幕样式在原仓库中使用的默认圆角。
/// </summary>
public static class SplashStyleDefaults
{
    /// <summary>
    /// 该样式是否带有可调整的圆角（全屏样式的圆角没有意义）。
    /// </summary>
    public static bool HasRoundedCorners(int splashKind) => splashKind is 1 or 2 or 3 or 6 or 7;

    /// <summary>
    /// 获取样式默认的圆角大小。
    /// </summary>
    public static double GetCornerRadius(int splashKind) => splashKind switch
    {
        2 or 3 => 8,
        7 => 20,
        1 or 6 => 4,
        _ => 0
    };
}
