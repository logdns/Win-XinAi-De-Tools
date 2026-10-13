# 更新记录 / Changelog

仅列出近期功能变化。历史版本与下载见 [GitHub Releases](https://github.com/logdns/Win-XinAi-De-Tools/releases)。
Recent feature changes are listed below; previous releases and downloads are available on GitHub.

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
