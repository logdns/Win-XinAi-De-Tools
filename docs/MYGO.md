# Win-XinAi-De-Tools-MyGo

**Built with MyGo · v1.11.0**

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
| 防火墙/网络/SMB/WSL | 原生操作选择器、参数表单与只读结果；复用原版系统服务与校验，不是完全相同的表格/标签页展示 |
| 规则导入导出 | 相同 JSON 格式；导入时粘贴 JSON，导出结果可复制/通过原生文件对话框保存 |
| 连接监控 | 手动查询；关闭连接需从查询结果复制单个连接 JSON，结束进程需填写 PID 并确认 |
| 临时 HTTP | 目录选择、启动/重启/停止、发现与结果地址；在原生结果区复制地址，不具备原版逐行地址按钮 |
| 外观与导航 | 中英文、系统/浅色/深色、历史返回、会话草稿、托盘、`/silent`；界面语言切换保留草稿 |
| 登录启动 | 本版不提供应用登录自启开关；WSL 发行版自启属于单独的系统功能 |

高级操作需先执行“刷新/查看”加载真实网卡、交换机或发行版列表。网络重置页另有“加载网卡列表”。所有修改均需确认；SMB Guest 共享可能降低整机安全，HTTP 无认证且允许外部访问。阅读结果中的错误、备份路径与重启提示，不要把请求完成当成已验证系统恢复。

此版同时包含 Go 原生前端及自包含 .NET 引擎；**不承诺单 EXE、小体积、特定内存/CPU 占用或与 Pulse 相同的性能**。

### 构建与验证

MyGo 固定为 `v0.5.6-0.20261016201914-e529e1a86f63`，使用 Go 1.27.2 和 .NET 8 SDK。在 Windows 仓库根目录示例：

```powershell
dotnet publish engine/Win-XinAi-De-Tools.Engine.csproj -c Release -r win-x64 --self-contained true -o artifacts/mygo-portable/win-x64/engine
cd mygo
go test ./...
go vet ./...
go run ./cmd/resources -arch amd64
$env:GOOS='windows'; $env:GOARCH='amd64'; $env:CGO_ENABLED='0'
go build -trimpath -ldflags='-s -w -H=windowsgui' -o ../artifacts/mygo-portable/win-x64/Win-XinAi-De-Tools-MyGo.exe .
cd ..
Copy-Item Assets artifacts/mygo-portable/win-x64/Assets -Recurse
```

完整发行包还需复制项目/上游许可，运行 `scripts/collect-go-licenses.py`；以 Actions 工作流为打包依据。ARM64 使用 `win-arm64` / `arm64`。不得把仅完成交叉编译描述为物理 ARM64 验收。

CI 的原生控件测试覆盖 12 页 × 2 语言 × 2 主题 × 3 尺寸；Windows x64 `scripts/mygo-ui-smoke.ps1` 启动实际窗口、只读探测真实引擎并捕获同样 144 个组合，另运行实际安装/快捷方式/卸载检查。MyGo 无障碍、混合 DPI、高对比度与真实 ARM64 设备仍需人工验收；不能继承 WinUI 的辅助技术兼容性结论。

引擎仅使用父子进程私有标准输入/输出管道、串行的固定操作白名单，不监听 TCP 或命名管道。`confirmed` 是同权限 GUI 的交互约定，不是隔离其他管理员的安全边界。一般操作最多等待 5 分钟，WSL 长操作 1 小时；超时中止引擎可能留下已执行修改或临时 HTTP 防火墙规则，须在本机检查清理。

## English

A separately installed, native Go/MyGo Windows edition, not a WebView wrapper. It preserves the WinUI release and shares its .NET system services through private inherited stdio pipes. Both processes run as the same administrator; the confirmation flag is a UI contract, not an authentication boundary.

Downloads support x64 and ARM64 only. Keep the entire package, including the self-contained .NET engine and licenses. Both installers provide independent default-selected desktop/Start Menu tasks and optional postinstall launch. App identities, installation paths and UI preferences are separate; Windows configuration, audit logs and network backups are shared. Avoid concurrent conflicting changes.

The interface uses typed native operation forms and readable results rather than recreating every WinUI table/tab. Rule import accepts pasted JSON; export can be saved through a native file dialog. Connection closing accepts one queried connection object; HTTP URLs are copied from the result area. Application login startup is not offered in this edition. Themes/languages, draft retention, Back, tray and `/silent` are supported. No claim is made about a tiny standalone binary, low CPU/memory, or performance matching Pulse.

Network reset offers DNS cache, selected-adapter IPv4 DHCP/automatic DNS and Winsock/TCP/IP modes. Every reset snapshots configuration before changing it, confirms with Cancel as default and never reboots automatically. Read [network reset and recovery](NETWORK-RESET.md) before use. SMB Guest sharing and unauthenticated external HTTP retain their explicit risks.

CI covers 144 native view combinations, all operation forms, confirmation contracts, Windows read-only engine/native captures and real x64 installer lifecycle. Actual network reset/reboot, physical ARM64, accessibility, mixed DPI and interactive installer presentation require manual acceptance. Five-minute general/one-hour WSL timeouts can interrupt already-applied changes; forced termination can require local HTTP firewall cleanup. Pinning, resource generation and complete packaging commands are in the workflow.
