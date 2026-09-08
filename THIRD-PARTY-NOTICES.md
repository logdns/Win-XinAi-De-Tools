# Third-Party Notices

## Microsoft and CommunityToolkit dependencies

The application references Microsoft.WindowsAppSDK, Microsoft.Windows.SDK.BuildTools, and CommunityToolkit.WinUI.Controls.SettingsControls through NuGet. These dependencies remain under their upstream licenses (the Windows App SDK and Windows Community Toolkit are MIT-licensed). Their source and license texts are available from the respective upstream repositories and NuGet packages.

应用通过 NuGet 引用 Microsoft.WindowsAppSDK、Microsoft.Windows.SDK.BuildTools 和 CommunityToolkit.WinUI.Controls.SettingsControls。这些依赖仍遵循各自上游许可（Windows App SDK 和 Windows Community Toolkit 使用 MIT 许可）。其源代码和许可文本请以对应上游仓库及 NuGet 包为准。

The WSL page is an independent native WinUI implementation that invokes Windows `wsl.exe`. No third-party WSL dashboard source or assets are bundled.

WSL 页面是调用 Windows `wsl.exe` 的独立原生 WinUI 实现，本项目不捆绑第三方 WSL 仪表板源代码或资源。

## SkiaSharp and Skia

The dashboard chart uses SkiaSharp 3.119.4 and SkiaSharp.NativeAssets.Win32 3.119.4. SkiaSharp is MIT licensed; Skia and its bundled components retain their respective licenses. Unmodified upstream license and third-party notice files from the native NuGet package are included in [licenses/](licenses/) and copied into portable and installer distributions.

概览图使用 SkiaSharp 与 Windows 原生库。上游许可和第三方声明原文保存在 `licenses/`，并随免安装及安装包分发。
