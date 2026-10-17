# Win-XinAi-De-Tools-MyGo

**Built with MyGo · v1.12.0**

[简体中文](#中文) | [English](#english)

## 中文

这是与原 WinUI 版并行维护的第二个 Windows 客户端，不是替换原版的网页皮肤。界面使用 [MyGo](https://github.com/egoist/mygo) 原生 Go GPU 控件，Windows 后端为 Direct3D 11，不使用 WebView。设计参考 [Pulse](https://pulse.egoist.dev/) 的克制分区和状态卡片，未复制其图片、图标或性能宣传。

### 安装与并行使用

- Windows 10 1809+；提供 **x64、ARM64** 的 ZIP 和 EXE，不提供 MyGo x86。原 WinUI 版继续提供 x86/x64/ARM64。
- 使用完整软件包，以管理员身份运行 `Win-XinAi-De-Tools-MyGo.exe`。不要单独复制 EXE：`engine/` 是必须保留的自包含 .NET 8 系统服务引擎，无需另装 .NET。
- 安装目录、AppId、桌面/开始菜单快捷方式与原版独立。两个快捷方式选项默认均勾选，安装完成后的“立即启动”单独选择；取消立即启动不会丢失入口。
- 界面设置：`%LOCALAPPDATA%\Win-XinAi-De-Tools-MyGo\preferences.json`；启动日志在同目录 `startup.log`。卸载/重装不会删除原版入口。
- **系统配置不是两份**：两版管理同一台电脑的网络、防火墙、Hyper-V、WSL、SMB；审计日志与网络备份共用原版数据目录。不要同时进行冲突的系统修改。两版 HTTP 服务不能占用同一端口。

### 能力与差异

| 项目 | MyGo 版行为 |
|---|---|
| 网络重置 | DNS 缓存、所选 GUID 网卡 DHCP/自动 DNS、Winsock/TCP/IP；操作前备份，取消为默认，不自动重启 |
| Hyper-V | 共用原版状态探测、功能/启动加载、虚拟网卡启停与受保护交换机操作 |
| 防火墙/网络/SMB/WSL | 专用原生功能页；直接展示规则、网卡配置、共享状态、发行版列表与对应按钮，进入页面自动只读加载；修改后回读 |
| 规则导入导出 | 相同 JSON 格式；原生文件选择、限 900 KB 的导入预览（最多展示 10 条）、可选粘贴 JSON；导出通过原生文件对话框保存 |
| 连接监控 | 进入自动查询，支持搜索与手动刷新；每条连接提供关闭 TCP / 结束进程按钮，确认锁定当时的连接对象或 PID，无需复制 JSON |
| 临时 HTTP | 目录选择、启动/重启/停止、运行状态与请求数、逐条地址复制/打开，以及本机服务发现 |
| 外观与导航 | 中英文、系统/浅色/深色、历史返回、会话草稿、托盘、`/silent`；界面语言切换保留草稿 |
| 登录启动 | 本版不提供应用登录自启开关；WSL 发行版自启属于单独的系统功能 |

v1.12.0 修复了旧版主要以“选择操作＋执行＋JSON”呈现的问题：现在各页直接展示实际状态与功能控件。进入页面会自动读取状态，仍可手动刷新；网络/SMB 未提交草稿保留，点击另一网卡会按该网卡重新填表。WSL 高级功能按安装、存储、任务、网络/USB 分组，独立展开表单。防火墙、连接与日志每页显示最多 50 项。原始响应默认折叠，仅供诊断。所有修改均需确认；SMB Guest 共享可能降低整机安全，HTTP 无认证且允许外部访问。阅读结果中的错误、备份路径与重启提示，不要把请求完成当成已验证系统恢复。

此版同时包含 Go 原生前端及自包含 .NET 引擎；**不承诺单 EXE、小体积、特定内存/CPU 占用或与 Pulse 相同的性能**。

### 构建与验证

MyGo 正式发布移除原生开发者检查器、关闭窗口 DevTools，并校验 MyGo SDK 与引擎/界面版本一致。MyGo 固定为 `v0.5.6-0.20261016201914-e529e1a86f63`，使用 Go 1.27.2 和 .NET 8 SDK。在 Windows 仓库根目录示例：

```powershell
dotnet publish engine/Win-XinAi-De-Tools.Engine.csproj -c Release -r win-x64 --self-contained true -o artifacts/mygo-portable/win-x64/engine
cd mygo
go test -tags=mygo_noinspector ./...
go vet -tags=mygo_noinspector ./...
go run ./cmd/resources -arch amd64
$env:GOOS='windows'; $env:GOARCH='amd64'; $env:CGO_ENABLED='0'
go build -tags=mygo_noinspector -trimpath -ldflags='-s -w -H=windowsgui -X github.com/egoist/mygo.production=1 -X github.com/egoist/mygo.packageIdentifier=com.logdns.winxinai.mygo -X github.com/egoist/mygo.packageName=Win-XinAi-De-Tools-MyGo -X github.com/egoist/mygo.packageVersion=1.12.0' -o ../artifacts/mygo-portable/win-x64/Win-XinAi-De-Tools-MyGo.exe .
cd ..
Copy-Item Assets artifacts/mygo-portable/win-x64/Assets -Recurse
```

完整发行包还需复制项目/上游许可，运行 `scripts/collect-go-licenses.py`；以 Actions 工作流为打包依据。ARM64 使用 `win-arm64` / `arm64`。不得把仅完成交叉编译描述为物理 ARM64 验收。

CI 的填充数据原生控件测试覆盖 12 页 × 2 语言 × 2 主题 × 3 尺寸，另验证实际内容、行按钮目标、确认默认取消、异步自动加载/回读/错误/草稿保留；Windows x64 `scripts/mygo-ui-smoke.ps1` 启动实际窗口、只读探测真实引擎并捕获同样 144 个组合及 20 张滚动后的设置/功能区域截图，另运行实际安装/快捷方式/卸载检查。MyGo 无障碍、混合 DPI、高对比度与真实 ARM64 设备仍需人工验收；不能继承 WinUI 的辅助技术兼容性结论。

引擎控制通道仅使用父子进程私有标准输入/输出管道、串行的固定操作白名单，不额外监听 TCP 或命名管道；主动启动的 HTTP 文件服务仍会监听所选端口。`confirmed` 是同权限 GUI 的交互约定，不是隔离其他管理员的安全边界。一般操作最多等待 5 分钟，WSL 长操作 1 小时；超时中止引擎可能留下已执行修改或临时 HTTP 防火墙规则，须在本机检查清理。

## English

A separately installed, native Go/MyGo Windows edition, not a WebView wrapper. It preserves the WinUI release and shares its .NET system services through private inherited stdio pipes. Both processes run as the same administrator; the confirmation flag is a UI contract, not an authentication boundary.

Downloads support x64 and ARM64 only. Keep the entire package, including the self-contained .NET engine and licenses. Both installers provide independent default-selected desktop/Start Menu tasks and optional postinstall launch. App identities, installation paths and UI preferences are separate; Windows configuration, audit logs and network backups are shared. Avoid concurrent conflicting changes.

Version 1.12.0 replaces the old operation-selector/JSON-first console with dedicated native feature pages: searchable firewall and connection lists with object-bound buttons, adapter settings populated from real IP/DNS/route data, Hyper-V features/adapters/switches, SMB status/addresses, WSL distribution actions and grouped advanced forms, and readable activity entries. Pages load read-only state on entry and read back after mutations, including partial failures. Network/SMB drafts are preserved; selecting a different adapter repopulates its form. Lists page at 50 items. Rule import uses native file selection (900 KB limit) and a 10-row preview, with optional pasted JSON; export saves through a native file dialog. HTTP URLs have individual copy/open controls. Raw responses remain collapsed diagnostics, never the primary feature view. Application login startup is not offered in this edition. Themes/languages, draft retention, Back, tray and `/silent` are supported. No claim is made about a tiny standalone binary, low CPU/memory, or performance matching Pulse.

Network reset offers DNS cache, selected-adapter IPv4 DHCP/automatic DNS and Winsock/TCP/IP modes. Every reset snapshots configuration before changing it, confirms with Cancel as default and never reboots automatically. Read [network reset and recovery](NETWORK-RESET.md) before use. SMB Guest sharing and unauthenticated external HTTP retain their explicit risks.

CI covers 144 populated native view combinations, every operation form, real-content assertions, row-target/default-cancel contracts, asynchronous page load/read-back/error/draft tests, Windows read-only engine/native captures (144 view combinations plus 20 scrolled settings views) and real x64 installer lifecycle. Actual network reset/reboot, physical ARM64, accessibility, mixed DPI and interactive installer presentation require manual acceptance. Five-minute general/one-hour WSL timeouts can interrupt already-applied changes; forced termination can require local HTTP firewall cleanup. Pinning, resource generation and complete packaging commands are in the workflow.


## v1.12.0 validation / 专用页面验收

The passed Windows candidate and published v1.12.0 tag workflows, downloaded package checks and manual limits are linked in [the focused review and validation record](AUDIT.md). Tests now assert populated feature content and exact row targets, not just titles. Native screenshots cover all 12 pages in both languages and explicit themes, plus scrolled settings. Requested 1400 × 900 windows are constrained by the hosted runner; actual capture sizes are recorded (maximum observed content 1028 × 749), not represented as a 1400-DIP hardware acceptance. The Static network form in screenshots is an unsubmitted draft; no network reset or Hyper-V mutation is executed. Virtual-adapter/distribution rows are empty when the real host has none; populated fixtures cover their controls without presenting invented state as real Windows data.


## v1.12.1 window and text fixes / 窗口与中文修复

- The first window is sized and centered inside the primary monitor's work area, in DIPs; taskbar space is excluded and the minimum size is capped for small displays. The normal Windows title bar, resize borders and system controls stay enabled.
- An always-visible native toolbar adds Minimize, Maximize/restore and Close/exit in both languages. Close means exit, not hide; the separate tray Hide command remains available. Active operations veto closing, including tray exit; normal shutdown closes the shared engine and its temporary HTTP service.
- Network adapter names/aliases and SMB paths/results use UTF-8 on both sides of the Windows PowerShell boundary. No Windows locale, global code page or adapter name is changed. WSL Linux output remains UTF-8, with BOM/UTF-16 launcher-error detection; disk-query failures show the actual diagnostic when available, never a fabricated success.
- 两版安装完成页保留可选立即启动，明确沿用安装器已提升的权限，避免 requireAdministrator 程序触发错误 740。网卡中文乱码不通过改名或替换字符掩盖；窗口工具栏始终提供最小化、最大化/还原、关闭/退出。

Release gates now include actual non-silent installer Finish-page launch on x64 and native MyGo Windows ARM64 execution/installation. The exact completed workflow and remaining device/DPI/manual limits are recorded in [AUDIT.md](AUDIT.md); source changes alone do not establish physical-device acceptance.
