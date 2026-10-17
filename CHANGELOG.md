# 更新记录 / Changelog

仅列出近期功能变化。历史版本与下载见 [GitHub Releases](https://github.com/logdns/Win-XinAi-De-Tools/releases)。
Recent feature changes are listed below; previous releases and downloads are available on GitHub.

## v1.12.0 - 2026-10-17

### MyGo functional interface / MyGo 功能界面修复

- Replace the generic operation selector and JSON-first console with dedicated native feature pages. Page entry automatically reads current system state; successful or partially failed mutations trigger read-back.
- Show searchable firewall/connection lists with object-bound row actions, adapter IPv4/DNS/route facts and populated settings, Hyper-V platform/features/virtual adapters/switches, SMB status/addresses and WSL distribution controls with grouped advanced forms.
- Present three distinct network reset cards and structured backup/step/restart results; retain Cancel-default confirmation, GUID scope and no automatic reboot.
- Add HTTP status/request counters and per-address copy/open controls, native JSON file import with bounded preview, rule file export and readable activity entries. Raw responses are collapsed diagnostic details only.
- Add populated-content UI assertions, exact row-target/default-cancel tests, real asynchronous automatic-load/read-back/error/draft tests, and Windows read-only probes plus screenshots for every MyGo page.
- Keep WinUI x86/x64/ARM64 and MyGo x64/ARM64 releases parallel, with independent installers and default-selected desktop/Start Menu shortcuts. No network reset or Hyper-V mutation is executed by CI.


## 1.11.1 · 2026-10-17

- MyGo 使用正式生产构建模式，移除原生开发者检查器，内置 SDK 包标识与版本并在实际窗口验证；未发布的 v1.11.0 标签保留，不覆盖。
- 新增本地网络重置：DNS 缓存、所选 GUID 网卡 DHCP/自动 DNS、整机 Winsock/TCP/IP；先备份、默认取消、逐项结果、仅提示手动重启，不重置防火墙或删除网卡/交换机。
- 新增 **Win-XinAi-De-Tools-MyGo / Built with MyGo** 原生 Go 客户端，与 WinUI 版同步发布；共用系统服务，独立安装目录、AppId、快捷方式和界面设置。
- MyGo 提供中英文/系统及深浅色主题、12 个原生页面、操作表单和结果、托盘、历史返回和会话草稿；支持 x64/ARM64，自包含 .NET 引擎随包分发，不是单 EXE 或 WebView 版本。
- 两版安装向导保留独立、默认勾选的桌面与开始菜单快捷方式及可选立即启动；新增双版安装/界面/依赖验证，统一发布校验文件。
- Added backed-up, explicitly confirmed local network reset and a side-by-side native MyGo edition. WinUI retains x86/x64/ARM64; MyGo supports x64/ARM64 with a bundled shared .NET engine. Both editions ship together with separate installers and one checksum manifest. MyGo uses operation forms/results, not identical WinUI tables; see the edition guide for differences and manual validation limits.

## 1.10.1 · 2026-10-13

- 安装向导提供独立的桌面与开始菜单快捷方式选项，默认均勾选；不立即启动软件也可找到入口。
- 开始菜单使用同名文件夹，包含程序与卸载入口；程序快捷方式明确设置图标与工作目录。
- 重新安装按本次选择更新快捷方式，迁移旧入口并避免沿用旧的取消勾选状态；增加实际 Windows 安装、快捷方式启动与卸载回归检查。
- Added independent, default-selected desktop and Start Menu shortcut choices, explicit icon/working-directory settings, legacy shortcut migration and Windows installer regression checks. Shortcut creation does not depend on launching after installation.

## 1.10.0 · 2026-10-13

- 新增中英文 Hyper-V 管理页面：功能状态、启用、仅关闭 Hyper-V 与关闭虚拟化平台及启动加载（手动重启生效）。
- 按驱动和 GUID 识别虚拟网卡，支持单独或全部启停，提供 IP/DNS 设置入口；不直接禁用物理网卡。
- 支持内部、专用、外部交换机创建，设置主机共享物理网卡；删除限于 WinXinAi- 前缀且无虚拟机连接的交换机，保护系统交换机。
- 所有修改均有安全默认的确认框与操作日志，说明断网、WSL2/Docker 和虚拟化安全影响，不清理系统 HNS/NAT 服务或虚拟磁盘。
- Added bilingual Hyper-V feature/boot management, GUID-scoped virtual adapter controls, guarded switch creation/removal, risk confirmations, and operation logging. Feature changes require a manual restart.

## 1.9.5 · 2026-09-18

- 设置 share 共享时自动开启无密码 Guest 模式，仅保留页面风险警示，不再弹出阻断式确认。
- Setting a share now enables passwordless Guest access immediately; the persistent warning remains without a blocking confirmation dialog.

## 1.9.4 · 2026-09-18

- SMB 共享默认开启无密码 Guest 访问，设置前显示高风险确认，移除共享时恢复 SMB 身份验证并停用 Guest。
- SMB sharing now enables passwordless Guest access by default with a prominent warning, and restores authenticated SMB access when the share is removed.

## 1.9.3 · 2026-09-18

- SMB 共享页明确说明 Windows 和 Mac 访问时需要共享主机上的 Windows 账户凭据；继续显示并复制 Win/Mac 链接。
- SMB sharing now explains the expected Windows-account authentication prompt on Windows and Mac while retaining the Win/Mac links and one-click copy.

## 1.9.2 · 2026-09-18

- SMB 共享页新增 Windows 和 Mac 访问地址展示，支持一键复制全部地址。
- SMB sharing now shows Windows and Mac access addresses with one-click copy for sharing configuration details.

## 1.9.1 · 2026-09-18

- SMB 共享设置新增本机文件夹选择器，选择后可直接设置 share 共享。
- SMB sharing now includes a local folder picker so a selected directory can be configured directly.

## 1.9.0 · 2026-09-09

- HTTP 地址支持逐条复制和打开；自动检测本机 HTTP 服务，显示端口、状态和进程，并支持手动刷新及启停后刷新。
- Individual HTTP URL actions; local server discovery with port, status, and process details, plus manual and lifecycle refresh.

## 1.8.0 · 2026-09-09

- 新增临时静态 HTTP 服务：默认 8980、自定义端口和目录、启停重启、外部访问与临时防火墙规则。
- Temporary static HTTP serving with configurable port/folder, lifecycle controls, external access, and temporary firewall rules.

## 1.7.0 · 2026-09-08

- 更新原生界面、深浅色主题、Skia 概览图形、自适应布局及按访问历史返回。
- Native UI update with saved themes, Skia dashboard graphics, responsive layouts, and history-based Back navigation.
