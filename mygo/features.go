package main

import (
	"encoding/json"
	"fmt"
	"strings"

	"github.com/egoist/mygo/ui"
)

func (a *application) featurePage(c *ui.Context) {
	a.operationSummary(c)
	switch a.page {
	case "dashboard":
		a.refreshButton(c)
		a.readState(c, "dashboard")
		a.overview(c)
	case "firewall":
		a.firewallPage(c)
	case "network":
		a.networkPage(c)
	case "reset":
		a.resetPage(c)
	case "hyperv":
		a.hypervPage(c)
	case "smb":
		a.smbPage(c)
	case "wsl":
		a.wslPage(c)
	case "http":
		a.httpPage(c)
	case "connections":
		a.connectionsPage(c)
	case "transfer":
		a.transferPage(c)
	case "audit":
		a.auditPage(c)
	}
}
func (a *application) refreshButton(c *ui.Context) {
	if ui.Button(c.Key("refresh-"+a.page), a.tr("刷新状态", "Refresh status")).Disabled(a.busy).Clicked() {
		a.status = ""
		a.failed = false
		a.refreshPage()
	}
}
func (a *application) readState(c *ui.Context, id string) bool {
	if message := a.readErrors[id]; message != "" {
		ui.Text(c, a.tr("读取失败：", "Unable to load: ")+message).TextColor(c.Theme().Danger)
		return false
	}
	if _, ok := a.data[id]; !ok {
		ui.Text(c, a.tr("尚未读取状态；进入页面后自动加载，也可点击刷新。", "Status not loaded. It loads on entry; you can also refresh.")).TextColor(c.Theme().TextMuted)
		return false
	}
	return true
}
func (a *application) empty(c *ui.Context) {
	ui.Text(c, a.tr("没有符合条件的项目。", "No matching items.")).TextColor(c.Theme().TextMuted)
}
func (a *application) fact(c *ui.Context, label, value string) {
	if value == "" {
		value = "—"
	}
	ui.Row(c).Gap(10).Wrap().Children(func() {
		ui.Text(c, label).TextColor(c.Theme().TextMuted)
		ui.Text(c, value)
	})
}
func (a *application) yes(value bool) string {
	if value {
		return a.tr("是", "Yes")
	}
	return a.tr("否", "No")
}
func (a *application) actionButton(c *ui.Context, id string, args map[string]string) {
	op, ok := actionByID(id)
	if !ok {
		return
	}
	if ui.Button(c.Key(id+formatArgs(args)), a.label(op)).Disabled(a.busy).Clicked() {
		if args == nil {
			a.request(op)
		} else {
			a.requestArgs(id, args)
		}
	}
}
func (a *application) form(c *ui.Context, id string) {
	op, ok := actionByID(id)
	if !ok {
		return
	}
	a.card(c, func() {
		ui.Text(c, a.label(op)+a.tr(" · 参数", " · Settings")).Bold().FontSize(18)
		for _, key := range actionFields(op) {
			if id == "hyperv.switch.create" && (key == "physicalId" || key == "share") && *a.value(id, "type") != "External" {
				continue
			}
			a.field(c, op, key)
		}
		if id == "smb.share" || id == "http.start" || id == "http.restart" {
			if ui.Button(c, a.tr("选择目录", "Choose folder")).Disabled(a.busy).Clicked() {
				a.pickDirectory(id, "path")
			}
		}
		if ui.PrimaryButton(c.Key("submit-"+id), a.label(op)).Disabled(a.busy).Clicked() {
			a.request(op)
		}
	})
}
func (a *application) disclosureForm(c *ui.Context, id string) {
	op, _ := actionByID(id)
	ui.Collapsible(c.Key(id), a.label(op), a.expanded(id), func() { a.form(c, id) })
}
func (a *application) pager(c *ui.Context, page *int, count int) (int, int) {
	const size = 50
	max := (count - 1) / size
	if *page > max {
		*page = max
	}
	if *page < 0 {
		*page = 0
	}
	ui.Row(c).Gap(10).Wrap().Children(func() {
		if ui.Button(c, a.tr("上一页", "Previous page")).Disabled(*page == 0).Clicked() {
			*page--
		}
		ui.Text(c, fmt.Sprintf("%d / %d · %d", *page+1, max+1, count))
		if ui.Button(c, a.tr("下一页", "Next page")).Disabled(*page >= max).Clicked() {
			*page++
		}
	})
	from := *page * size
	to := from + size
	if to > count {
		to = count
	}
	return from, to
}

func (a *application) firewallPage(c *ui.Context) {
	a.refreshButton(c)
	ui.TextInput(c.Key("rule-search"), &a.uiState.ruleFilter).Placeholder(a.tr("搜索规则、端口或协议", "Search rules, ports or protocol")).Label(a.tr("搜索规则、端口或协议", "Search rules, ports or protocol"))
	directionKeys := []string{"All", "Inbound", "Outbound"}
	directionLabels := []string{a.tr("全部方向", "All directions"), a.tr("入站", "Inbound"), a.tr("出站", "Outbound")}
	selectedDirection := directionLabels[0]
	for i, key := range directionKeys {
		if a.uiState.direction == key {
			selectedDirection = directionLabels[i]
		}
	}
	if ui.Select(c.Key("direction-filter"), &selectedDirection, directionLabels).Label(a.tr("方向筛选", "Direction filter")).Changed() {
		for i, label := range directionLabels {
			if selectedDirection == label {
				a.uiState.direction = directionKeys[i]
			}
		}
	}
	a.disclosureForm(c, "firewall.add")
	ui.Collapsible(c.Key("port-query"), a.tr("按端口查询规则", "Query rules by port"), a.expanded("port-query"), func() {
		a.card(c, func() {
			op, _ := actionByID("firewall.query")
			a.field(c, op, "port")
			a.actionButton(c, op.ID, nil)
			if rows, ok := readData[[]firewallRule](a, "firewall.query"); ok {
				ui.Text(c, fmt.Sprintf(a.tr("查询匹配：%d 条", "Query matches: %d"), len(rows)))
				for _, r := range rows {
					ui.Text(c, r.Name+" · "+r.Direction+" · "+r.protocol()+" · "+r.port())
				}
			}
		})
	})
	if a.readState(c, "firewall.list") {
		rules, _ := readData[[]firewallRule](a, "firewall.list")
		filtered := []firewallRule{}
		for _, r := range rules {
			if a.uiState.direction != "" && a.uiState.direction != "All" && r.Direction != a.uiState.direction {
				continue
			}
			if !strings.Contains(strings.ToLower(r.Name+" "+r.port()+" "+r.protocol()), strings.ToLower(a.uiState.ruleFilter)) {
				continue
			}
			filtered = append(filtered, r)
		}
		from, to := a.pager(c, &a.uiState.rulePage, len(filtered))
		for i := from; i < to; i++ {
			r := filtered[i]
			a.card(c.Key(fmt.Sprint("rule-", i)), func() {
				ui.Text(c, r.Name).Bold()
				a.fact(c, a.tr("方向 / 协议 / 端口", "Direction / protocol / port"), r.Direction+" / "+r.protocol()+" / "+r.port())
				a.fact(c, a.tr("已启用 / 配置文件", "Enabled / profile"), r.Enabled+" / "+r.Profile)
				ui.Row(c).Gap(8).Wrap().Children(func() {
					if ui.Button(c, a.tr("编辑规则", "Edit rule")).Disabled(a.busy).Clicked() {
						a.editRule(r)
					}
					a.actionButton(c, "firewall.delete", map[string]string{"name": r.Name})
				})
				if *a.expanded("rule-editor") && *a.value("firewall.modify", "oldName") == r.Name {
					a.form(c, "firewall.modify")
				}
			})
		}
		if len(filtered) == 0 {
			a.empty(c)
		}
	}

}
func (a *application) editRule(r firewallRule) {
	for key, val := range map[string]string{"oldName": r.Name, "name": r.Name, "port": r.port(), "protocol": r.protocol(), "direction": "in"} {
		a.setField("firewall.modify", key, val)
	}
	if r.Direction == "Outbound" {
		a.setField("firewall.modify", "direction", "out")
	}
	*a.expanded("rule-editor") = true
}

func (a *application) networkPage(c *ui.Context) {
	a.refreshButton(c)
	if !a.readState(c, "network.list") {
		return
	}
	rows, _ := readData[[]networkAdapter](a, "network.list")
	if len(rows) == 0 {
		a.empty(c)
		return
	}
	labels := []string{}
	selected := ""
	for _, r := range rows {
		label := r.Name + " · " + r.Status
		labels = append(labels, label)
		if r.InterfaceGuid == a.uiState.networkID {
			selected = label
		}
	}
	if ui.Select(c.Key("network-adapter"), &selected, labels).Label(a.tr("选择网卡", "Select adapter")).Disabled(a.busy).Changed() {
		for _, r := range rows {
			if selected == r.Name+" · "+r.Status {
				a.uiState.networkID = r.InterfaceGuid
				a.uiState.networkConfigID = ""
				a.runRead("network.read", map[string]string{"adapterId": r.InterfaceGuid})
				break
			}
		}
	}
	for _, r := range rows {
		if r.InterfaceGuid == a.uiState.networkID {
			a.fact(c, "GUID / MAC", r.InterfaceGuid+" / "+r.MacAddress)
		}
	}
	if a.readErrors["network.read"] != "" {
		a.readState(c, "network.read")
		return
	}
	if a.uiState.networkConfigID != a.uiState.networkID {
		ui.Text(c, a.tr("正在读取所选网卡配置……", "Reading selected adapter configuration…"))
		return
	}
	config, ok := readData[networkConfig](a, "network.read")
	if !ok {
		return
	}
	a.card(c, func() {
		ui.Text(c, config.InterfaceAlias).FontSize(20).Bold()
		a.fact(c, "IPv4", fmt.Sprintf("%s/%d", config.IPv4Address, config.PrefixLength))
		a.fact(c, "DHCP", a.yes(config.DhcpEnabled))
		a.fact(c, a.tr("网关", "Gateway"), config.Gateway)
		a.fact(c, "DNS", strings.Join(config.DnsServers, ", "))
		a.fact(c, a.tr("路由跃点", "Route metric"), fmt.Sprint(config.DefaultRouteMetric))
	})
	a.card(c, func() {
		ui.Text(c, a.tr("IP 与 DNS 设置", "IP and DNS settings")).Bold()
		if ui.Button(c, a.tr("重新填入当前配置（放弃草稿）", "Reload current settings (discard draft)")).Disabled(a.busy).Clicked() {
			a.uiState.networkConfigID = ""
			a.runRead("network.read", map[string]string{"adapterId": a.uiState.networkID})
		}
		op, _ := actionByID("network.apply")
		if *a.value(op.ID, "mode") == "DHCP" {
			ui.Text(c, a.tr("DHCP 模式同时恢复自动 DNS；不会应用手动地址或 DNS。", "DHCP mode also restores automatic DNS; manual addresses and DNS are not applied."))
		}
		for _, key := range actionFields(op) {
			if key == "adapterId" {
				continue
			}
			if *a.value(op.ID, "mode") == "DHCP" && key != "mode" {
				continue
			}
			a.field(c, op, key)
		}
		a.actionButton(c, op.ID, nil)
	})
}

func (a *application) resetPage(c *ui.Context) {
	a.refreshButton(c)
	modes := []struct{ id, zh, en, detailZH, detailEN string }{
		{"FlushDns", "清理 DNS 缓存", "Flush DNS cache", "不更改 IP、DNS 服务器或网卡。", "Leaves IP settings, DNS servers and adapters unchanged."},
		{"AdapterDhcp", "恢复所选网卡自动配置", "Restore adapter DHCP", "移除所选网卡手动 IPv4 / 默认路由，恢复 DHCP 和自动 DNS。", "Removes manual IPv4/default route on the selected adapter; restores DHCP and automatic DNS."},
		{"SystemStack", "重置 Winsock / TCP/IP", "Reset Winsock / TCP/IP", "影响全机、VPN 和虚拟网络；完成后需要手动重启。", "Affects the entire host, VPN and virtual networks. Restart manually afterwards."},
	}
	for _, mode := range modes {
		a.card(c.Key(mode.id), func() {
			ui.Text(c, a.tr(mode.zh, mode.en)).Bold().FontSize(19)
			ui.Text(c, a.tr(mode.detailZH, mode.detailEN))
			ready := true
			if mode.id == "AdapterDhcp" {
				ready = a.readState(c, "network.list") && len(a.choices["adapterId"]) > 0
				op, _ := actionByID("network.reset")
				a.field(c, op, "adapterId")
				valid := false
				adapters, _ := readData[[]networkAdapter](a, "network.list")
				for _, adapter := range adapters {
					valid = valid || (adapter.InterfaceGuid == *a.value(op.ID, "adapterId") && adapter.Status != "Disabled")
				}
				ready = ready && valid
			}
			if ui.Button(c, a.tr("执行：", "Run: ")+a.tr(mode.zh, mode.en)).Disabled(a.busy || !ready).Clicked() {
				id := ""
				if mode.id == "AdapterDhcp" {
					id = *a.value("network.reset", "adapterId")
				}
				a.requestArgs("network.reset", map[string]string{"mode": mode.id, "adapterId": id})
			}
		})
	}
	if result, ok := readData[resetResult](a, "network.reset"); ok {
		a.card(c, func() {
			a.fact(c, a.tr("备份文件（供手工恢复）", "Backup file (manual recovery)"), result.BackupPath)
			if ui.Button(c, a.tr("复制备份路径", "Copy backup path")).Clicked() {
				c.WriteClipboard(result.BackupPath)
			}
			a.fact(c, a.tr("需要手动重启", "Manual restart required"), a.yes(result.RestartRequired))
			for _, step := range result.Steps {
				color := c.Theme().Success
				if !step.Success {
					color = c.Theme().Danger
				}
				ui.Text(c, step.Name+" · "+a.yes(step.Success)).TextColor(color)
				ui.Text(c, step.Output)
			}
		})
	}
}

func (a *application) hypervPage(c *ui.Context) {
	a.refreshButton(c)
	if !a.readState(c, "hyperv.status") {
		return
	}
	status, ok := readData[hyperStatus](a, "hyperv.status")
	if !ok {
		return
	}
	a.card(c, func() {
		ui.Text(c, a.tr("平台与启动状态", "Platform and boot status")).Bold().FontSize(20)
		a.fact(c, a.tr("Hypervisor 已加载", "Hypervisor present"), a.yes(status.HypervisorPresent))
		a.fact(c, "hypervisorlaunchtype", status.BootLaunchType)
		a.fact(c, a.tr("Hyper-V 管理模块可用", "Management module available"), a.yes(status.ManagementAvailable))
		for _, feature := range status.Features {
			a.fact(c, feature.Name, feature.State)
		}
		ui.Row(c).Gap(8).Wrap().Children(func() {
			for _, id := range []string{"hyperv.enable", "hyperv.disable", "hyperv.complete"} {
				a.actionButton(c, id, nil)
			}
		})
	})
	a.card(c, func() {
		ui.Text(c, a.tr("Hyper-V 虚拟网卡", "Hyper-V virtual adapters")).Bold().FontSize(20)
		if status.NetworkError != "" {
			ui.Text(c, status.NetworkError).TextColor(c.Theme().Danger)
		}
		if len(status.VirtualAdapters) == 0 && status.NetworkError == "" {
			a.empty(c)
		}
		if len(status.VirtualAdapters) > 0 && status.NetworkError == "" {
			ids := []string{}
			for _, row := range status.VirtualAdapters {
				ids = append(ids, row.ID)
			}
			ui.Row(c).Gap(8).Wrap().Children(func() {
				for _, enable := range []bool{true, false} {
					label := a.tr("启用全部 Hyper-V 网卡", "Enable all Hyper-V adapters")
					if !enable {
						label = a.tr("禁用全部 Hyper-V 网卡", "Disable all Hyper-V adapters")
					}
					if ui.Button(c, label).Disabled(a.busy).Clicked() {
						a.requestArgs("hyperv.adapters", map[string]string{"ids": strings.Join(ids, ","), "enable": fmt.Sprint(enable)})
					}
				}
			})
		}
		for _, r := range status.VirtualAdapters {
			ui.Column(c.Key(r.ID)).Gap(8).Children(func() {
				a.fact(c, r.Name, r.Status)
				a.fact(c, "GUID", r.ID)
				ui.Row(c).Gap(8).Wrap().Children(func() {
					for _, enable := range []bool{true, false} {
						label := a.tr("启用网卡", "Enable adapter")
						if !enable {
							label = a.tr("禁用网卡", "Disable adapter")
						}
						if ui.Button(c, label).Disabled(a.busy || status.NetworkError != "").Clicked() {
							a.requestArgs("hyperv.adapters", map[string]string{"ids": r.ID, "enable": fmt.Sprint(enable)})
						}
					}
				})
			})
		}
	})
	a.card(c, func() {
		ui.Text(c, a.tr("虚拟交换机", "Virtual switches")).Bold().FontSize(20)
		if status.SwitchError != "" {
			ui.Text(c, status.SwitchError).TextColor(c.Theme().Danger)
		}
		if len(status.Switches) == 0 && status.SwitchError == "" {
			a.empty(c)
		}
		for _, r := range status.Switches {
			ui.Column(c.Key(r.ID)).Gap(8).Children(func() {
				a.fact(c, r.Name, r.SwitchType)
				a.fact(c, a.tr("主机共享", "Management OS sharing"), a.yes(r.AllowManagementOS))
				a.fact(c, "GUID", r.ID)
				if strings.HasPrefix(strings.ToLower(r.Name), "winxinai-") {
					a.actionButton(c, "hyperv.switch.remove", map[string]string{"id": r.ID})
				}
			})
		}
	})
	if status.ManagementAvailable {
		a.disclosureForm(c, "hyperv.switch.create")
	}
}

func (a *application) smbPage(c *ui.Context) {
	a.refreshButton(c)
	if a.readState(c, "smb.status") {
		status, _ := readData[smbStatus](a, "smb.status")
		a.card(c, func() {
			ui.Text(c, a.tr("当前 SMB 配置", "Current SMB configuration")).Bold().FontSize(20)
			a.fact(c, "SMB Direct", a.yes(status.SmbDirectEnabled))
			a.fact(c, "SMB 1.0", a.yes(status.Smb1Enabled))
			a.fact(c, a.tr("共享已建立", "Share exists"), a.yes(status.ShareExists))
			a.fact(c, status.ShareName, status.SharePath)
			if status.ShareExists {
				a.actionButton(c, "smb.remove", nil)
			}
		})
		a.form(c, "smb.features")
		if status.ShareExists && a.readState(c, "smb.addresses") {
			addresses, _ := readData[[]smbAddress](a, "smb.addresses")
			a.card(c, func() {
				for _, r := range addresses {
					a.copyAddress(c, r.WindowsPath, false)
					a.copyAddress(c, r.MacUrl, false)
				}
			})
		}
	}
	a.form(c, "smb.share")
}

func (a *application) wslPage(c *ui.Context) {
	a.refreshButton(c)
	if a.readState(c, "wsl.status") {
		status, _ := readData[wslStatus](a, "wsl.status")
		a.fact(c, a.tr("已安装 WSL", "WSL installed"), a.yes(status.IsInstalled))
		for _, r := range status.Distributions {
			a.card(c.Key(r.Name), func() {
				ui.Text(c, r.Name).Bold().FontSize(20)
				a.fact(c, a.tr("状态 / 版本", "State / version"), fmt.Sprintf("%s / WSL %d", r.State, r.Version))
				a.fact(c, a.tr("默认发行版", "Default distribution"), a.yes(r.IsDefault))
				ui.Row(c).Gap(8).Wrap().Children(func() {
					for _, id := range []string{"wsl.start", "wsl.stop", "wsl.default", "wsl.terminal", "wsl.explorer", "wsl.code", "wsl.disk"} {
						a.actionButton(c, id, map[string]string{"distribution": r.Name})
					}
				})
			})
		}
		if len(status.Distributions) == 0 {
			ui.Text(c, a.tr("尚未安装 Linux 发行版。可在安装与更新中选择发行版安装。", "No Linux distributions installed. Choose a distribution in Setup to install one."))
		}
	}
	if disk, ok := readData[string](a, "wsl.disk"); ok {
		a.card(c, func() { ui.Text(c, a.tr("最近查询的磁盘占用", "Last disk query")).Bold(); ui.Text(c, disk) })
	}
	tabs := []string{a.tr("安装与更新", "Setup"), a.tr("备份与存储", "Storage"), a.tr("自启与任务", "Tasks"), a.tr("网络与 USB", "Network / USB")}
	// Wrapping buttons keep every section accessible at the minimum width.
	ui.Row(c).Gap(8).Wrap().Children(func() {
		for i, title := range tabs {
			if ui.Button(c, title).Clicked() {
				a.uiState.wslTab = i
			}
		}
	})
	ui.Text(c, tabs[a.uiState.wslTab]).FontSize(20).Bold()
	groups := [][]string{
		{"wsl.install", "wsl.update", "wsl.shutdown", "wsl.online", "wsl.version", "wsl.install-distro"},
		{"wsl.export", "wsl.import", "wsl.move", "wsl.mount", "wsl.unmount", "wsl.unregister"},
		{"wsl.autostart", "wsl.schedule", "wsl.unschedule"},
		{"wsl.port.add", "wsl.port.remove", "wsl.proxy", "wsl.usb", "wsl.usb.bind", "wsl.usb.attach", "wsl.usb.detach"},
	}
	for _, id := range groups[a.uiState.wslTab] {
		op, _ := actionByID(id)
		if len(actionFields(op)) == 0 {
			a.actionButton(c, id, nil)
		} else {
			a.disclosureForm(c, id)
		}
	}
	for _, id := range []string{"wsl.online", "wsl.version", "wsl.usb"} {
		if text, ok := readData[string](a, id); ok {
			ui.Text(c, text)
		}
		if rows, ok := readData[[]string](a, id); ok {
			for _, r := range rows {
				ui.Text(c, r)
			}
		}
	}
}

func (a *application) copyAddress(c *ui.Context, address string, open bool) {
	ui.Column(c.Key(address)).Gap(6).Children(func() {
		ui.Text(c, address)
		ui.Row(c).Gap(8).Wrap().Children(func() {
			if ui.Button(c, a.tr("复制地址", "Copy address")).Clicked() {
				c.WriteClipboard(address)
			}
			if open && (strings.HasPrefix(address, "http://") || strings.HasPrefix(address, "https://")) && ui.Button(c, a.tr("打开地址", "Open address")).Clicked() {
				c.OpenURL(address)
			}
		})
	})
}
func (a *application) httpPage(c *ui.Context) {
	a.refreshButton(c)
	if a.readState(c, "http.status") {
		status, _ := readData[httpStatus](a, "http.status")
		a.card(c, func() {
			ui.Text(c, a.tr("文件服务状态", "File server status")).Bold().FontSize(20)
			a.fact(c, a.tr("运行中", "Running"), a.yes(status.IsRunning))
			a.fact(c, a.tr("请求数", "Requests"), fmt.Sprint(status.RequestCount))
			if status.NeedsFirewallCleanup {
				ui.Text(c, a.tr("需要清理残留防火墙规则，请点击停止。", "Firewall cleanup required. Click Stop.")).TextColor(c.Theme().Danger)
			}
			if status.Active != nil {
				a.fact(c, a.tr("目录", "Directory"), status.Active.Directory)
				a.fact(c, a.tr("端口", "Port"), fmt.Sprint(status.Active.Port))
			}
			for _, address := range status.URLs {
				a.copyAddress(c, address, true)
			}
			if status.IsRunning || status.NeedsFirewallCleanup {
				a.actionButton(c, "http.stop", nil)
			}
		})
		if status.IsRunning {
			a.disclosureForm(c, "http.restart")
		} else {
			a.form(c, "http.start")
		}
	}
	a.card(c, func() {
		ui.Text(c, a.tr("本机 HTTP 服务发现", "Local HTTP service discovery")).Bold()
		a.actionButton(c, "http.discover", nil)
		if scan, ok := readData[httpScan](a, "http.discover"); ok {
			if scan.IsPartial {
				ui.Text(c, a.tr("扫描部分完成，结果可能不完整。", "Partial scan; results may be incomplete.")).TextColor(c.Theme().Warning)
			}
			if len(scan.Servers) == 0 {
				a.empty(c)
			}
			for _, r := range scan.Servers {
				a.copyAddress(c, r.URL, true)
				a.fact(c, "HTTP / process", fmt.Sprintf("%d / %s", r.StatusCode, r.ProcessName))
			}
		}
	})
}

func (a *application) connectionsPage(c *ui.Context) {
	a.refreshButton(c)
	ui.TextInput(c.Key("connection-search"), &a.uiState.connectionFilter).Placeholder(a.tr("搜索进程、PID、地址或端口", "Search process, PID, address or port")).Label(a.tr("搜索进程、PID、地址或端口", "Search process, PID, address or port"))
	if !a.readState(c, "connections.list") {
		return
	}
	rows, _ := readData[[]connection](a, "connections.list")
	filtered := []connection{}
	for _, r := range rows {
		text := fmt.Sprintf("%s %d %s %d %s %d %s", r.ProcessName, r.ProcessId, r.LocalAddress, r.LocalPort, r.RemoteAddress, r.RemotePort, r.Protocol)
		if strings.Contains(strings.ToLower(text), strings.ToLower(a.uiState.connectionFilter)) {
			filtered = append(filtered, r)
		}
	}
	from, to := a.pager(c, &a.uiState.connectionPage, len(filtered))
	if len(filtered) == 0 {
		a.empty(c)
	}
	for i := from; i < to; i++ {
		r := filtered[i]
		a.card(c.Key(fmt.Sprint("connection-", i)), func() {
			ui.Text(c, fmt.Sprintf("%s · PID %d", r.ProcessName, r.ProcessId)).Bold()
			a.fact(c, r.Protocol, r.State)
			a.fact(c, a.tr("本地", "Local"), fmt.Sprintf("%s:%d", r.LocalAddress, r.LocalPort))
			a.fact(c, a.tr("远程", "Remote"), fmt.Sprintf("%s:%d", r.RemoteAddress, r.RemotePort))
			ui.Row(c).Gap(8).Wrap().Children(func() {
				if strings.EqualFold(r.Protocol, "TCP") && r.State != "Listen" {
					data, _ := json.Marshal(r)
					a.actionButton(c, "connections.close", map[string]string{"connection": string(data)})
				}
				if r.ProcessId > 4 {
					a.actionButton(c, "connections.terminate", map[string]string{"pid": fmt.Sprint(r.ProcessId)})
				}
			})
		})
	}
}

func (a *application) transferPage(c *ui.Context) {
	a.card(c, func() {
		ui.Text(c, a.tr("导出当前规则", "Export current rules")).Bold().FontSize(20)
		a.actionButton(c, "firewall.export", nil)
		if text, ok := readData[string](a, "firewall.export"); ok {
			var doc ruleDocument
			if json.Unmarshal([]byte(text), &doc) == nil {
				a.fact(c, a.tr("导出规则数量", "Exported rule count"), fmt.Sprint(len(doc.Rules)))
			}
			if ui.Button(c, a.tr("保存规则文件", "Save rule file")).Disabled(a.busy).Clicked() {
				a.saveResult(text)
			}
		}
	})
	a.card(c, func() {
		ui.Text(c, a.tr("导入规则文件", "Import rule file")).Bold().FontSize(20)
		if ui.Button(c, a.tr("选择 JSON 文件", "Choose JSON file")).Disabled(a.busy).Clicked() {
			a.pickRuleFile()
		}
		text := a.value("firewall.import", "json")
		ui.Collapsible(c, a.tr("粘贴或查看 JSON", "Paste or inspect JSON"), a.expanded("import-json"), func() { ui.TextArea(c, text).Height(180).Disabled(a.busy) })
		if *text != "" {
			doc, err := parseRulePreview(*text)
			if err != nil {
				ui.Text(c, err.Error()).TextColor(c.Theme().Danger)
			} else {
				a.fact(c, a.tr("待导入规则", "Rules to import"), fmt.Sprint(len(doc.Rules)))
				for i, r := range doc.Rules {
					if i >= 10 {
						break
					}
					ui.Text(c, r.Name+" · "+r.Direction+" · "+r.protocol()+" · "+r.port())
				}
				ui.Text(c, a.tr("预览最多 10 条；完整校验由共享引擎执行。", "Preview shows up to 10 rules. The shared engine performs final validation."))
				a.actionButton(c, "firewall.import", nil)
			}
		}
	})
}
func parseRulePreview(text string) (ruleDocument, error) {
	var doc ruleDocument
	if err := json.Unmarshal([]byte(text), &doc); err != nil {
		return doc, err
	}
	if doc.FormatVersion != 1 {
		return doc, fmt.Errorf("Unsupported formatVersion: %d", doc.FormatVersion)
	}
	for _, r := range doc.Rules {
		if strings.TrimSpace(r.Name) == "" || strings.TrimSpace(r.Direction) == "" || strings.TrimSpace(r.Protocol) == "" {
			return doc, fmt.Errorf("Every rule needs Name, Dir and Proto")
		}
	}
	return doc, nil
}
func (a *application) auditPage(c *ui.Context) {
	ui.Row(c).Gap(8).Wrap().Children(func() { a.refreshButton(c); a.actionButton(c, "audit.clear", nil) })
	if !a.readState(c, "audit.list") {
		return
	}
	rows, _ := readData[[]auditEntry](a, "audit.list")
	from, to := a.pager(c, &a.uiState.logPage, len(rows))
	if len(rows) == 0 {
		a.empty(c)
	}
	for i := from; i < to; i++ {
		r := rows[i]
		a.card(c.Key(fmt.Sprint(i)), func() {
			ui.Text(c, r.Action).Bold()
			color := c.Theme().Success
			label := a.tr("成功", "Success")
			if !r.Success {
				color = c.Theme().Danger
				label = a.tr("失败", "Failed")
			}
			ui.Text(c, label+" · "+r.Timestamp).TextColor(color)
			ui.Text(c, r.Details)
		})
	}
}
func (a *application) operationSummary(c *ui.Context) {
	// Mutations expose structured outcomes without making JSON the primary UI.
	for _, op := range actions[a.page] {
		if op.Read || op.ID == "network.reset" {
			continue
		}
		data := a.data[op.ID]
		if len(data) == 0 {
			continue
		}
		var result operationResult
		if json.Unmarshal(data, &result) != nil || (result.Message == "" && result.ErrorMessage == "" && result.SuccessCount == 0 && result.FailedCount == 0) {
			continue
		}
		a.card(c, func() {
			ui.Text(c, a.label(op)).Bold()
			if result.Message != "" {
				ui.Text(c, result.Message)
			}
			if result.ErrorMessage != "" {
				ui.Text(c, result.ErrorMessage).TextColor(c.Theme().Danger)
			}
			a.fact(c, a.tr("成功 / 失败", "Succeeded / failed"), fmt.Sprintf("%d / %d", result.SuccessCount, result.FailedCount))
		})
	}
}
