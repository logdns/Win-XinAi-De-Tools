# Win-XinAi-De-Tools

[English](README.md) | **简体中文**

Win-XinAi-De-Tools 是一款原生 Windows 工具，用于管理网络配置、Windows Defender 防火墙规则、SMB 共享和适用于 Linux 的 Windows 子系统（WSL）。项目使用 WinUI 3、Windows App SDK 和 Windows Community Toolkit 构建，界面支持在运行时切换英文和简体中文。

## 当前版本

**v1.9.0** — [从 GitHub Releases 下载](https://github.com/logdns/Win-XinAi-De-Tools/releases/tag/v1.9.0)

版本记录请参阅 [CHANGELOG.md](CHANGELOG.md)。

- 临时 HTTP 页面支持逐条复制/打开地址，进入时自动检测本机已开启的 HTTP 服务，并提供手动刷新、端口和进程信息。

## 功能

- 临时 HTTP 服务：默认 8980、自定义端口、选择/打开目录、启动/停止/重启，监听所有网卡并自动管理临时防火墙规则。详见 [使用方法及外网访问](docs/TEMPORARY-HTTP.md)。

- 现代原生 UI：支持并保存跟随系统、深色和浅色外观，弹窗同步主题，页面动画遵循 Windows 动画设置。
- Skia 绘制的规则分布图，全部 14 个页面自适应布局、操作栏换行，以及按访问历史返回的按钮与 Alt+← 快捷键。

- 添加、查询、列出和删除 TCP、UDP 或 ANY 防火墙规则，支持入站、出站和双向流量。
- 配置网卡 IPv4、DHCP、DNS、网关、路由跃点和默认路由，并提供校验及确认提示。
- 开关 SMB Direct 和 SMB 1.0/CIFS，可选择立即重启 Windows，并管理固定名称 `share` 的文件夹共享。
- 监控 TCP 和 UDP 活动连接并关联进程，关闭 TCP 连接或在确认后终止进程。
- 使用 JSON 备份和恢复防火墙规则，并查看追加式审计日志。
- 检测 WSL 安装和运行状态，浏览及安装发行版、导入 TAR 存档、启动或停止发行版、设置默认发行版、刷新状态并打开终端。
- 终止或注销发行版、查看磁盘使用、在资源管理器或 VS Code 中打开、导入或导出 TAR、挂载 VHDX、迁移发行版、安排命令、配置 HTTP 代理或 IPv4 端口转发，以及管理 usbipd-win 设备。
- 支持 `/silent` 参数、登录自启、启动时最小化到通知区域，以及退出应用时可选关闭 WSL。

## 界面预览

以下图片由界面验证流程从 Windows 构建产物的真实运行界面截取：

![浅色概览页](docs/screenshots/dashboard.png)

![深色概览页](docs/screenshots/dashboard-dark.png)

![WSL 管理页预览](docs/screenshots/wsl-manager.png)

侧栏底部可选择主题和语言，重启后自动恢复。返回按钮回到实际上一页；切换主题保留草稿，切换语言会重建页面并重置未提交的表单。原生控件由 WinUI 渲染，概览图形由 Skia 绘制。详细行为、验证矩阵和发布流程请参阅 [界面与验证说明](docs/UI-AND-VALIDATION.md)。

## WSL 集成

WSL 页面是本仓库实现的原生 WinUI 管理界面。未安装 WSL 时，界面会提供提升权限的 `wsl.exe --install` 操作，等待安装结束、报告退出结果并链接到微软安装指南。发行版面板始终提供 WSL 在线目录和 TAR 导入入口。

运行状态取自 WSL 的运行中发行版名单，不依赖本地化状态文本；Linux 命令输出按 UTF-8 解码。发行版操作与高级设置分别位于响应式、可滚动的标签页中。

该工作流借鉴了常见 WSL 仪表板的设计思路，但项目为独立实现，不复制或捆绑第三方仪表板源代码和资源。

## 安装

应用需要 Windows 10 版本 1809（Build 17763）或更高版本。一键执行 `wsl --install` 需要 Windows 10 版本 2004（Build 19041）或更高版本；更早但受支持的系统需要按照微软的手动 WSL 安装指南操作。修改防火墙、网络、SMB 以及安装 WSL 需要管理员权限。

请选择与 Windows 架构匹配的软件包：

| 架构 | 免安装 ZIP | 安装包 |
|---|---|---|
| x86 | [Win-XinAi-De-Tools-win-x86.zip](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-win-x86.zip) | [Win-XinAi-De-Tools-Setup-x86.exe](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-Setup-x86.exe) |
| x64 | [Win-XinAi-De-Tools-win-x64.zip](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-win-x64.zip) | [Win-XinAi-De-Tools-Setup-x64.exe](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-Setup-x64.exe) |
| ARM64 | [Win-XinAi-De-Tools-win-arm64.zip](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-win-arm64.zip) | [Win-XinAi-De-Tools-Setup-arm64.exe](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-Setup-arm64.exe) |

免安装版本为自包含程序，不需要另外安装 .NET 或 Windows App SDK。解压 ZIP 后，以管理员身份运行 `Win-XinAi-De-Tools.exe`。安装包会创建开始菜单快捷方式，也可以选择创建桌面快捷方式。

## 构建与测试

请在 Windows 上构建，并安装 Visual Studio 2022、.NET 桌面和 Windows App SDK 工作负载、.NET 8 SDK，以及 Windows SDK 10.0.19041.0 或更高版本。

```powershell
dotnet publish Win-XinAi-De-Tools.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:Platform=x64 `
  -p:WindowsPackageType=None `
  --output artifacts\portable\win-x64

dotnet test Win-XinAi-De-Tools.Tests\Win-XinAi-De-Tools.Tests.csproj --configuration Release
```

将 `win-x64`/`x64` 替换为 `win-x86`/`x86` 或 `win-arm64`/`ARM64` 可构建其他架构。Windows 防火墙集成测试必须在提升权限的 Windows 会话中运行。

## GitHub Actions

[`.github/workflows/build.yml`](.github/workflows/build.yml) 会运行 .NET 与原生辅助组件测试、Windows 防火墙集成测试、x86/x64/ARM64 构建、x64 GUI 和通知区域冒烟测试、全页面主题/布局/导航检查与截图、直接及传递依赖漏洞审计、免安装 ZIP 打包、Inno Setup 安装包构建，并为版本匹配的 `v*` 标签发布 Release。第三方 Action 已固定到不可变提交，仅发布任务拥有仓库写权限。

## 项目结构

- `Views/`：原生 WinUI 页面。
- `Services/`：防火墙、网络、SMB、WSL、连接、传输、审计和通知区域服务。
- `Models/`：领域模型。
- `Win-XinAi-De-Tools.Tests/`：自动化测试。
- `installer/`：Inno Setup 定义。
- `Assets/`：应用图标和资源。
- `native/`：可选的 Rust 和 Go WSL 桥接程序。

应用默认直接调用 `wsl.exe`。设置 `WINXINAI_WSL_HELPER` 可使用已编译的可选桥接程序，构建命令请参阅 [native/README.md](native/README.md)。

## 安全

仅在需要时以管理员身份运行应用。SMB 1.0/CIFS 是存在已知安全风险的旧协议。只导入可信的 JSON 文件。网络、进程和连接信息只在本机读取，不会发送到远程服务。

## 参与贡献与许可证

提交 Issue 或 Pull Request 时，请附上 Windows 版本、系统架构、应用版本、复现步骤，以及 `%LOCALAPPDATA%\Win-XinAi-De-Tools\startup.log` 或 `audit.log` 中的相关内容。公开日志前请移除隐私信息。开发说明请参阅 [CONTRIBUTING.md](CONTRIBUTING.md)。

Win-XinAi-De-Tools 使用 [MIT License](LICENSE) 发布。第三方声明请参阅 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
