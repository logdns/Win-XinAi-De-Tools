# Win-XinAi-De-Tools

[English](README.md) | **简体中文**

原生 Windows 网络工具，集中管理防火墙、网络配置、SMB、WSL 和临时 HTTP 服务。支持中英文、深浅色主题、自适应布局与返回导航；使用 WinUI 3 原生控件和 Skia 概览图形。

## 下载

**v1.9.0** · [GitHub Releases](https://github.com/logdns/Win-XinAi-De-Tools/releases/latest) · [Actions](https://github.com/logdns/Win-XinAi-De-Tools/actions/workflows/build.yml)

需要 Windows 10 1809 或更高版本。解压便携 ZIP 或运行安装程序，以管理员身份启动应用。软件包自包含，无需另装 .NET 或 Windows App SDK。

| 架构 | 便携版 | 安装版 |
|---|---|---|
| x64 | [ZIP](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-win-x64.zip) | [EXE](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-Setup-x64.exe) |
| x86 | [ZIP](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-win-x86.zip) | [EXE](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-Setup-x86.exe) |
| ARM64 | [ZIP](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-win-arm64.zip) | [EXE](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/Win-XinAi-De-Tools-Setup-arm64.exe) |

[SHA256SUMS.txt](https://github.com/logdns/Win-XinAi-De-Tools/releases/download/v1.9.0/SHA256SUMS.txt) 用于校验下载文件。

## 功能

- **临时 HTTP**：默认端口 8980，可自定义端口、选择/打开目录、启动/停止/重启；逐条复制和打开地址，自动检测本机 HTTP 服务并显示端口和进程。支持外部访问，详见 [HTTP 使用指南](docs/TEMPORARY-HTTP.md)。
- **防火墙与网络**：管理规则，查询端口，配置网卡、DNS 和路由，监控连接和进程，导入/导出规则与查看操作日志。
- **SMB**：管理 SMB Direct、SMB 1.0/CIFS 和共享目录。
- **WSL**：安装与管理发行版，打开终端，导入/导出和迁移，管理存储、代理、端口转发与 USB 设备。
- **桌面体验**：保存主题和语言，按访问历史返回，支持托盘、登录自启与 `/silent`。

修改系统配置需要管理员权限。SMB 1.0 属于旧协议，请按需启用。一键安装 WSL 需要 Windows 10 2004 或更新版本。

## 界面

![概览](docs/screenshots/dashboard.png)

![本机 HTTP 服务检测](docs/screenshots/temporary-http-discovery.png)

## 文档与开发

- [HTTP 使用与外网访问](docs/TEMPORARY-HTTP.md)
- [界面行为与验证](docs/UI-AND-VALIDATION.md)
- [构建、测试与贡献](CONTRIBUTING.md)
- [当前审计与验证记录](docs/AUDIT.md)
- [近期更新](CHANGELOG.md) · [历史版本](https://github.com/logdns/Win-XinAi-De-Tools/releases)

使用 [MIT License](LICENSE) 发布；第三方许可见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
