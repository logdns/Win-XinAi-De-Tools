# Hyper-V 设置与网络 / Hyper-V and networking

## Requirements / 前提

以管理员身份运行，使用本地会话。Hyper-V 主机功能通常需要支持虚拟化的 Windows Pro、Enterprise 或 Education，以及已在固件中启用虚拟化的 CPU。Home、32 位系统或其他架构/版本可能没有该功能；应用仍可显示可用的虚拟化组件，不能绕过 Windows 的限制。应用不会自动修改固件设置。

Run as administrator in a local session. Hyper-V host features generally require a supported Windows Pro, Enterprise or Education edition and firmware-enabled CPU virtualization. Home, 32-bit systems and other edition/architecture combinations may not expose the feature. The app shows available components; it does not bypass Windows requirements or configure firmware.

## Three different controls / 三类控制

| Operation | Effect / 行为 |
|---|---|
| 启用 / Enable Hyper-V | Enable `Microsoft-Hyper-V-All` with dependencies; set current boot entry `hypervisorlaunchtype=auto`; restart manually. 不会恢复 WSL2 等组件的原有设置。 |
| 仅关闭功能 / Feature-only disable | Disable `Microsoft-Hyper-V-All`, preserving `VirtualMachinePlatform`, `HypervisorPlatform` and the boot setting. WSL2/Docker may still use the hypervisor. 重启后生效，不代表虚拟化彻底关闭。 |
| 彻底关闭 / Complete disable | Disable those three optional features if present; set current boot entry `hypervisorlaunchtype=off`; restart manually. A pending feature change blocks the operation until restart. 安全策略或其他虚拟网络可能仍存在，不是清空所有网络的保证。 |
| 虚拟网卡启停 / Adapter enable/disable | Immediate, GUID-scoped change to selected/all Hyper-V host adapters, including previously disabled adapters. Does not remove switches, NAT, HNS networks or physical bindings. Windows may recreate/re-enable them. 临时诊断，不是持久关闭。 |

“当前运行”来自 Windows 的 `HypervisorPresent`；“下次启动”来自当前启动项的 BCD 设置。两者可能在重启前不同。`Default` 表示 BCD 没有显式值，不是对下一次实际运行状态的保证。`Unavailable` 是组件不可用；`EnablePending` / `DisablePending` 需要重启。状态查询失败不表示网络正常或已关闭。

Runtime hypervisor state and requested next-boot launch state are distinct. `Default` means no explicit BCD launch setting, not a guarantee of the next runtime state. Unavailable components and pending changes are shown, and query failures do not imply a healthy or disabled network.

彻底关闭会影响 WSL2、Docker、Windows Sandbox 与虚拟化安全（VBS/Credential Guard 等）。不会删除虚拟磁盘或 WSL 发行版，但请先保存工作、关闭虚拟机并退出 Docker。此工具不停止 HNS/vmcompute、不清除 NAT，不修改 VBS/Device Guard/组策略，不强制停止虚拟机，不自动重启。

Complete disable affects WSL2, Docker, Windows Sandbox and virtualization-based security. Disks/distributions are not deleted. Save work and stop VMs/Docker first. The tool does not stop HNS/vmcompute, purge NAT, change VBS/Device Guard/group policies, forcibly stop VMs or restart the machine.

## Virtual adapters / 虚拟网卡

通过 Windows `Win32_NetworkAdapter.ServiceName=VMSMP` 驱动及 `InterfaceGuid` 识别，并在操作前重新核实所有所选 GUID。不信任 `vEthernet` 等可编辑名称；物理网卡不会被此启停功能直接禁用。名称包含特殊字符或被重命名也不会改变目标。

The tool identifies adapters by the VMSMP driver and interface GUID, and revalidates the entire selected set before mutation. Renamed aliases and physical NIC names cannot redirect virtual-adapter operations. Operations can still partially fail after validation; no automatic rollback is promised.

**外部交换机的主机流量可能经过虚拟网卡。关闭该虚拟网卡仍会使电脑断网，即使物理网卡本身没有被禁用。** 不要通过依赖该网络的远程桌面或 SSH 操作。可以重新选择对应虚拟网卡启用；确认所有应用允许后才使用“全部启用”。启用虚拟网卡后，在“网络设置”页面选择它配置 IP、DNS 和路由。已禁用网卡不出现在原网络设置页面中。

**Disabling an external switch's host virtual adapter can disconnect the computer even though its physical NIC is not disabled.** Avoid RDP/SSH over that path. Re-enable the corresponding adapter locally. Enable an adapter before selecting it in Network settings to edit IP/DNS/routes.

## Switches / 交换机

- Internal / 内部：连接主机与虚拟机，不自动配置 NAT、DHCP 或互联网访问。
- Private / 专用：仅连接虚拟机，主机不参与。
- External / 外部：绑定到重新核实 GUID 的物理网卡；“允许主机共享”默认开启。取消后主机不能经此物理网卡联网，创建或删除交换机可能改变主机 IP/DNS 或立即断网。Windows 不支持的绑定会返回原始错误。
- 新建名称统一为 `WinXinAi-<输入名称>`。Windows cmdlet 接受通配符，故拒绝交换机名称中的 `* ? [ ]`；物理网卡名称含这些字符时也拒绝外部创建，要求先重命名。
- 删除只允许 `WinXinAi-` 前缀，并在服务层重新核实 ID 和名称。任何虚拟机网卡（包括关机的虚拟机）仍连接时拒绝删除。先在 Hyper-V Manager 断开这些连接。
- Windows Default Switch、WSL、Docker 和其他无此前缀的交换机只显示，不提供删除。此前缀不是所有权凭证；已有此前缀交换机也会被视作可管理，请核实用途。工具不自动连接虚拟机，不提供 NAT 清理或导入旧拓扑。

Removal is limited to WinXinAi-prefixed switches without guest VM adapters attached. System and other non-prefixed switches are read-only. The prefix is a management convention, not proof of provenance. VMs are never automatically attached; NAT/DHCP is never automatically installed.

## Recovery / 恢复

1. 网络断开时，本地打开工具并启用原虚拟网卡；不要猜测或删除系统交换机。检查物理网卡及虚拟网卡原有 IP/DNS。
2. 使用“启用 Hyper-V”恢复 Hyper-V 和启动自动加载，再手动重启。WSL2 的 VirtualMachinePlatform、Sandbox 等已关闭组件需按原用途分别重新启用；本工具没有保存完整的旧系统状态。
3. 若确实需要恢复 WSL2，可在管理员 PowerShell 执行以下命令并手动重启；只有原来使用 Windows Hypervisor Platform 的应用才需要第二个命令：

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -All -NoRestart
# Only when required by the original workload:
Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All -NoRestart
bcdedit /set '{current}' hypervisorlaunchtype auto
```

4. 部分失败会显示警告并尽量刷新状态。DISM/BCD 没有跨命令事务；不要直接重复操作，先检查待重启状态，必要时重启。
5. 降级应用或安装旧版本**不会撤销系统配置**。受策略控制的设备应联系管理员，不要强行关闭安全策略。

Recover locally by enabling the original adapter, verifying IP/DNS and re-enabling only the optional components your workloads previously used. DISM/BCD and adapter batches are not transactional. Failed or timed-out operations may be partial. Restart pending changes before retrying. Downgrading the app does not restore OS settings; policy-managed devices need administrator review.

## Validation / 验收边界

CI covers generated PowerShell syntax, mocked GUID selection, preflight rejection and switch-removal guards without changing Hyper-V host configuration. UI smoke visits this page in both languages/themes at three sizes and checks draft retention. Hosted runners do **not** prove real Hyper-V networking or post-reboot behavior.

请在可恢复的 Windows 测试机执行硬件验收：开启/关闭/重启后状态、缺少功能的系统、WSL2/Docker 兼容、内部/专用/外部交换机、主机共享开关、改名网卡/禁用网卡、系统交换机保护、已连接虚拟机拒绝删除、断网后本地恢复、权限错误及部分失败。Physical x86/ARM64, firmware, policy, external networking and reboot behavior remain environment-specific checks; do not treat CI success as those checks passing.
