# 本地网络重置 / Local network reset

WinUI：网络配置页面底部。MyGo：网络重置页面。两版调用相同服务。

**仅在可以本地操作的电脑上执行。重置可能立即断开 Wi-Fi、VPN、远程桌面与 SSH；不要通过目标网卡进行远程验收。** 先记录固定 IP、DNS、网关、VPN 和企业策略，保存工作。

## 三种范围

| 选项 | 修改范围 | 重启 |
|---|---|---|
| 清理 DNS 缓存 / FlushDns | `Clear-DnsClientCache`；不改地址、路由或网卡状态 | 不要求 |
| 所选网卡 DHCP / AdapterDhcp | 按 GUID 重新识别网卡，删除该接口的手动 IPv4 地址和手动默认路由，启用 IPv4 DHCP，并将该接口 DNS 设为自动（包括该接口 IPv6 DNS） | 通常不要求；需要可用 DHCP 服务 |
| Winsock/TCP/IP / SystemStack | 依次执行系统 `netsh winsock reset`、`netsh int ip reset`，对整机网络栈生效，可能改变多个接口设置 | 提示手动重启；不会自动重启 |

所有模式执行前均采集网卡、IP/前缀、DHCP、DNS 与路由快照，写入：

`%LOCALAPPDATA%\Win-XinAi-De-Tools\network-backups\<时间-随机ID>\network.json`

备份采集或写入失败则不执行重置。该 JSON **仅供人工参考，不是完整系统镜像、凭据备份或自动回滚包**。系统栈模式另保留 `tcpip-reset.log`（由 Windows 命令生成）。网卡模式拒绝缺失/禁用的 GUID；不以用户可修改的显示名称选择目标。

## 确认与结果

- 默认选择“取消”；确认后检查逐项输出、备份路径与重启提示。
- 两条 netsh 命令独立记录退出码和输出；一项失败不能报告全部成功。Windows 命令也可能输出局部失败信息，即使退出码为 0，仍须阅读输出并验证当前配置。
- 不重置防火墙、不卸载/删除网卡、不删除 Hyper-V 交换机、不清理 HNS/NAT、不删除 WSL/虚拟磁盘，也不承诺修复路由器、运营商或物理链路故障。
- 系统策略或安全软件可能恢复/阻止设置；执行成功不代表互联网一定可用。失败或超时可能已经部分修改系统，不要盲目重复。

## 本地恢复

1. 在本地打开 Windows 网络设置，检查目标网卡是否启用、Wi-Fi 是否已连接，按需要重新连接 VPN。
2. 原先使用 DHCP 的网络，等待租约后核对地址/默认网关/DNS；原先固定地址的网络，参照 JSON 与事前记录，通过本工具网络配置或 Windows 设置人工恢复。
3. 系统栈模式完成后保存工作并手动重启；重启前后都检查 IP、路由、DNS、VPN、WSL2/Docker/Hyper-V。
4. 企业设备请联系管理员恢复策略/认证。安装回旧版软件不会撤销系统配置修改。备份含本机网络信息，对外提交前脱敏。

## 验证边界

单元测试验证模式、GUID、范围与失败结果；Windows CI 用 PowerShell 解析器和模拟 cmdlet 验证作用域及过期目标保护，GUI 只检查布局与只读查询。**CI 不执行真实 DNS/DHCP/Winsock/TCP/IP 重置。** 在有本地控制台的可丢弃 Windows 设备上分别验收三种模式、备份失败、禁用/移除网卡、静态地址恢复、DHCP 不可用及手动重启。物理 x86/ARM64 与企业 VPN 兼容性不得由 x64 CI 推断。

## English

Use the bottom of Network settings (WinUI) or Network reset (MyGo). DNS mode only clears the cache. Adapter mode revalidates a GUID, removes that interface's manual IPv4 addresses/default routes, enables IPv4 DHCP and resets its DNS server configuration, including IPv6 DNS. System mode runs separately checked Winsock and TCP/IP reset commands and requests a manual reboot.

Every mode writes a configuration snapshot before changing Windows. The JSON is a manual reference, not automatic rollback or a complete backup. Cancel is the default. Inspect each command's output even when its exit code is zero; partial changes are possible on failure/timeout. No automatic reboot, firewall reset, adapter uninstall, switch removal or HNS/NAT purge occurs.

Never perform this over the affected remote connection. Restore static addressing from your records and snapshot locally; reconnect Wi-Fi/VPN and manually reboot after system reset. CI uses parser/mocked-cmdlet tests and read-only GUI checks, not real resets. Destructive/reset/reboot recovery requires a disposable, locally accessible Windows machine.
