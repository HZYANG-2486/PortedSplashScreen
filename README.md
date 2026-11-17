
# MoreSplashScreen

向 ClassIsland 添加更丰富多彩的启动画面！

## 特性

添加了以下样式的启动屏幕：

- Android Studio
- Microsoft 365 [^1]
- Microsoft Office（旧） [^1]
- IslandOnTop
- Adobe Photoshop [^2]

[^1]: Microsoft、Microsoft 365 和 Microsoft Office 是 Microsoft Corporation 的商标。
[^2]: © 1990-2024 Adobe. All rights reserved. Adobe, the Adobe logo, Adobe Photoshop are either registered trademarks or trademarks of Adobe in the United States and/or other countries.

## 需求

本插件需要 1.6.1.0 及以上版本的 ClassIsland。

## 使用方法

1. 在插件市场安装本插件
2. 在[【应用设置】->【更多启动屏幕】](classisland://app/settings/dev.hellowrc.classisland.noMoreSplash.splashSettings)设置你喜欢的启动屏幕样式
3. 重启应用使设置生效

## 许可

本项目基于 GNU General Public License v3.0 许可。

Foremist © 2025 by DannyFeng is licensed under CC BY-NC-ND 4.0. To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0/

## 背景素材的按需下载

内置启动背景（Android Studio、Adobe Photoshop 两种启动屏幕使用的插画）为第三方作品，**不随本插件分发**。插件会在需要时自动从原版仓库下载：

- 下载源：`raw.githubusercontent.com/HelloWRC/MoreSplashScreen`，失败时回退到 jsDelivr 镜像。
- 下载后按内置的 SHA-256 校验，校验通过才会写入缓存目录并使用；校验失败或网络不可用时不使用任何替代图片。
- 缓存位置：插件配置目录下的 `AssetsCache/`，同时作为离线可用缓存。
- 可在「应用设置 → 更多启动屏幕 → 按需下载背景素材」中关闭自动下载；关闭后仅使用已有缓存，缺失时启动画面显示浅色渐变占位背景。
- 在设置页切换启动屏幕类型时，若所需素材尚未缓存，会等待下载完成后再恢复操作，保证重启应用后能直接用上原版仓库的图片。
- Adobe Photoshop 启动屏幕也支持自定义背景：在「更多启动屏幕」中打开「自定义启动屏幕背景」，选择本地图片即可；文件不可用时会回退到内置的 Foremist 背景。
