package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"strings"

	"github.com/egoist/mygo"
	"github.com/egoist/mygo/ui"
)

const version = "1.11.1"

type preferences struct {
	Language string `json:"language"`
	Theme    string `json:"theme"`
}

type pendingOperation struct {
	Action      action
	Args        map[string]string
	Description string
}
type choice struct{ Label, Value string }
type application struct {
	win                      *mygo.Window
	engine                   *engineClient
	prefs                    preferences
	page                     string
	history                  []string
	fields                   map[string]*string
	choices                  map[string][]choice
	selected                 map[string]*string
	results                  map[string]string
	status                   string
	busy, failed, confirming bool
	pending                  pendingOperation
	fixture                  bool
	stats                    struct {
		Rules    int `json:"rules"`
		Inbound  int `json:"inbound"`
		Outbound int `json:"outbound"`
	}
}

func newApplication() *application {
	a := &application{page: "dashboard", fields: map[string]*string{}, choices: map[string][]choice{}, selected: map[string]*string{}, results: map[string]string{}, prefs: preferences{Language: "zh-CN", Theme: "System"}}
	if data, err := os.ReadFile(preferencePath()); err == nil {
		_ = json.Unmarshal(data, &a.prefs)
	}
	if a.prefs.Language != "en-US" {
		a.prefs.Language = "zh-CN"
	}
	if a.prefs.Theme != "Light" && a.prefs.Theme != "Dark" {
		a.prefs.Theme = "System"
	}
	return a
}

func preferencePath() string {
	root := os.Getenv("LOCALAPPDATA")
	if root == "" {
		root, _ = os.UserConfigDir()
	}
	return filepath.Join(root, "Win-XinAi-De-Tools-MyGo", "preferences.json")
}

func (a *application) savePreferences() {
	if a.fixture {
		return
	}
	path := preferencePath()
	data, _ := json.Marshal(a.prefs)
	if err := os.MkdirAll(filepath.Dir(path), 0700); err != nil {
		a.status = err.Error()
		a.failed = true
		return
	}
	file, err := os.CreateTemp(filepath.Dir(path), "preferences-*.tmp")
	if err == nil {
		_, err = file.Write(data)
		closeErr := file.Close()
		if err == nil {
			err = closeErr
		}
		if err == nil {
			err = os.Rename(file.Name(), path)
		}
		_ = os.Remove(file.Name())
	}
	if err != nil {
		a.status = err.Error()
		a.failed = true
	}
}

func (a *application) tr(zh, en string) string {
	if a.prefs.Language == "en-US" {
		return en
	}
	return zh
}
func (a *application) label(action action) string { return a.tr(action.ZH, action.EN) }
func (a *application) value(op, key string) *string {
	id := op + ":" + key
	if a.fields[id] == nil {
		value := defaultValue(op, key)
		a.fields[id] = &value
	}
	return a.fields[id]
}
func (a *application) navigate(id string) {
	if id == a.page || a.confirming {
		return
	}
	a.history = append(a.history, a.page)
	a.page = id
}

func (a *application) view(c *ui.Context) {
	theme := *c.Theme()
	if a.prefs.Theme == "Light" {
		theme = *ui.LightTheme()
	} else if a.prefs.Theme == "Dark" {
		theme = *ui.DarkTheme()
	}
	theme.Accent = ui.Hex("#167562")
	theme.AccentHover = ui.Hex("#1b8b73")
	theme.AccentPressed = ui.Hex("#105b4c")
	theme.Font = "Segoe UI"
	theme.FontSize = 14
	theme.Radius = 8
	c.SetTheme(&theme)
	width, _ := c.Size()
	ui.Row(c).Fill().Background(theme.Background).Children(func() {
		if width >= 760 {
			a.sidebar(c)
		}
		ui.Column(c).Grow(1).MinWidth(0).FillHeight().Children(func() {
			ui.Row(c).Padding(14, 22).Gap(10).Background(theme.Surface).BorderWidth(0, 0, 1, 0).BorderColor(theme.Border).Children(func() {
				if ui.Button(c, a.tr("返回", "Back")).Disabled(len(a.history) == 0 || a.busy).Clicked() {
					a.page = a.history[len(a.history)-1]
					a.history = a.history[:len(a.history)-1]
				}
				ui.Text(c, "Win-XinAi-De-Tools-MyGo").Bold().Grow(1)
				ui.Text(c, "v"+version).TextColor(theme.TextMuted)
			})
			if width < 760 {
				labels := []string{}
				selected := ""
				for _, p := range pages {
					label := a.tr(p.ZH, p.EN)
					labels = append(labels, label)
					if p.ID == a.page {
						selected = label
					}
				}
				if ui.Select(c.Key("compact-navigation"), &selected, labels).Label(a.tr("页面", "Page")).Margin(12).Changed() {
					for _, p := range pages {
						if selected == a.tr(p.ZH, p.EN) {
							a.navigate(p.ID)
						}
					}
				}
			}
			ui.Scroll(c.Key(a.page)).Grow(1).Padding(24).Gap(18).Children(func() {
				for _, p := range pages {
					if p.ID == a.page {
						ui.Text(c, a.tr(p.ZH, p.EN)).FontSize(30).Bold()
						ui.Text(c, a.tr(p.DescriptionZH, p.DescriptionEN)).TextColor(theme.TextMuted)
					}
				}
				if a.page == "settings" {
					a.settings(c)
					return
				}
				if a.page == "dashboard" {
					a.overview(c)
				}
				if warning := a.warning(a.page); warning != "" {
					a.card(c, func() {
						ui.Text(c, a.tr("操作边界与风险", "Safety and scope")).Bold().TextColor(theme.Warning)
						ui.Text(c, warning)
					})
				}
				a.operationPanel(c)
				if a.status != "" {
					a.card(c, func() {
						color := theme.Success
						if a.failed {
							color = theme.Danger
						}
						ui.Text(c, a.status).TextColor(color)
					})
				}
				if a.busy {
					ui.Text(c, a.tr("正在执行，请勿关闭程序……", "Working. Keep the application open…")).TextColor(theme.TextMuted)
				}
				if output := a.results[a.page]; output != "" {
					a.card(c, func() {
						ui.Row(c).Gap(8).Children(func() {
							ui.Text(c, a.tr("结果 / 配置详情", "Results / configuration details")).Bold().Grow(1)
							if ui.Button(c, a.tr("复制结果", "Copy results")).Clicked() {
								c.WriteClipboard(output)
							}
							if ui.Button(c, a.tr("保存结果", "Save results")).Disabled(a.busy).Clicked() {
								a.saveResult(output)
							}
						})
						ui.TextArea(c.Key("result"), &output).ReadOnly(true).Height(300).Label(a.tr("操作结果", "Operation results"))
					})
				}
			})
		})
	})
	// The last alert button is the default in MyGo: Cancel stays the safe default.
	if ui.AlertDialog(c, &a.confirming, a.tr("确认系统操作", "Confirm system operation"), a.pending.Description, a.tr("确认执行", "Confirm operation"), a.tr("取消", "Cancel")) == 0 {
		a.perform(a.pending.Action, a.pending.Args, true)
	}
}

func (a *application) sidebar(c *ui.Context) {
	t := c.Theme()
	ui.Column(c).Width(232).FillHeight().Padding(18, 12).Gap(16).Background(t.Surface).BorderWidth(0, 1, 0, 0).BorderColor(t.Border).Children(func() {
		ui.Column(c).Padding(8).Gap(4).Children(func() {
			ui.Text(c, "XINAI / NETWORK").Bold().FontSize(16)
			ui.Text(c, "Built with MyGo").FontSize(12).TextColor(t.Accent)
		})
		selected := a.page
		if ui.Sidebar(c, &selected, func() {
			for _, p := range pages {
				ui.SidebarItem(c, p.ID, nil, a.tr(p.ZH, p.EN))
			}
		}).Grow(1).Changed() {
			a.navigate(selected)
		}
		ui.Text(c, a.tr("本机 Windows 系统工具", "Windows system tools")).FontSize(12).TextColor(t.TextMuted)
		ui.Select(c.Key("sidebar-language"), &a.prefs.Language, []string{"zh-CN", "en-US"}).Label("Language / 语言").OnChange(a.savePreferences)
		ui.Select(c.Key("sidebar-theme"), &a.prefs.Theme, []string{"System", "Light", "Dark"}).Label(a.tr("外观", "Appearance")).OnChange(a.savePreferences)
	})
}

func (a *application) card(c *ui.Context, body func()) {
	ui.Column(c).Padding(18).Gap(12).Radius(12).Background(c.Theme().Surface).Border(1, c.Theme().Border).Children(body)
}

func (a *application) overview(c *ui.Context) {
	ui.Row(c).Gap(12).Wrap().Children(func() {
		for _, metric := range []struct {
			zh, en string
			n      int
		}{{"规则总数", "Rules", a.stats.Rules}, {"入站规则", "Inbound", a.stats.Inbound}, {"出站规则", "Outbound", a.stats.Outbound}} {
			ui.Column(c).Grow(1).MinWidth(150).Padding(20).Gap(8).Radius(12).Gradient(c.Theme().Surface, c.Theme().Accent.Alpha(0.09), 35).Border(1, c.Theme().Border).Children(func() {
				ui.Text(c, a.tr(metric.zh, metric.en)).TextColor(c.Theme().TextMuted)
				value := "—"
				if a.results["dashboard"] != "" {
					value = fmt.Sprint(metric.n)
				}
				ui.Text(c, value).FontSize(38).Bold()
			})
		}
	})
	a.card(c, func() {
		ui.Text(c, a.tr("原生界面，共享可靠的系统服务", "Native interface. Shared system services.")).FontSize(18).Bold()
		ui.Text(c, a.tr("MyGo 版和 WinUI 版可同时安装；系统网络、规则与发行版是同一份，修改会影响两个版本。MyGo 界面不使用 WebView，但此版本包含自包含 .NET 服务引擎，不承诺小体积或特定 CPU 占用。", "Install alongside WinUI. Both editions manage the same Windows configuration. No WebView is used; this edition includes a self-contained .NET service engine, with no small-download or CPU-use claim."))
		ui.Row(c).Gap(8).Wrap().Children(func() {
			if ui.Button(c, a.tr("打开网络重置", "Open network reset")).Clicked() {
				a.navigate("reset")
			}
			if ui.Button(c, a.tr("管理 Hyper-V", "Manage Hyper-V")).Clicked() {
				a.navigate("hyperv")
			}
		})
	})
}

func (a *application) settings(c *ui.Context) {
	a.card(c, func() {
		ui.Text(c, "Win-XinAi-De-Tools-MyGo "+version).FontSize(20).Bold()
		ui.Text(c, "Built with MyGo").TextColor(c.Theme().Accent)
		ui.Select(c, &a.prefs.Language, []string{"zh-CN", "en-US"}).Label("Language / 语言").OnChange(a.savePreferences)
		ui.Select(c, &a.prefs.Theme, []string{"System", "Light", "Dark"}).Label(a.tr("外观", "Appearance")).OnChange(a.savePreferences)
		ui.Text(c, a.tr("Go 原生 GPU 界面 / 共享 .NET 服务引擎 / 独立设置 / MIT 许可。界面借鉴 Pulse 的克制分区与状态卡片，未复制其图标、截图或性能宣传。", "Go native GPU UI / shared .NET engine / independent preferences / MIT. Inspired by Pulse's calm sections and status cards; no Pulse icons, screenshots or performance claims are copied."))
		if ui.Button(c, a.tr("项目文档", "Project documentation")).Clicked() {
			c.OpenURL("https://github.com/logdns/Win-XinAi-De-Tools")
		}
		if ui.Button(c, "MyGo").Clicked() {
			c.OpenURL("https://github.com/egoist/mygo")
		}
	})
}

func (a *application) operationPanel(c *ui.Context) {
	group := actions[a.page]
	if len(group) == 0 {
		return
	}
	if a.selected[a.page] == nil {
		value := group[0].ID
		a.selected[a.page] = &value
	}
	selected := a.selected[a.page]
	a.card(c, func() {
		labels := []string{}
		current := ""
		chosen := group[0]
		for _, op := range group {
			label := a.label(op)
			labels = append(labels, label)
			if op.ID == *selected {
				current = label
				chosen = op
			}
		}
		if ui.Select(c.Key("operation"), &current, labels).Label(a.tr("操作", "Operation")).Disabled(a.busy).Changed() {
			for _, op := range group {
				if current == a.label(op) {
					*selected = op.ID
					chosen = op
				}
			}
		}
		for _, key := range actionFields(chosen) {
			a.field(c, chosen, key)
		}
		ui.Row(c).Gap(10).Wrap().Children(func() {
			if ui.PrimaryButton(c, a.tr("执行所选操作", "Run selected operation")).Disabled(a.busy).Clicked() {
				a.request(chosen)
			}
			if a.page == "reset" && ui.Button(c, a.tr("加载网卡列表", "Load adapters")).Disabled(a.busy).Clicked() {
				op, _ := actionByID("network.list")
				a.perform(op, nil, false)
			}
			if a.page == "http" && ui.Button(c, a.tr("选择服务目录", "Choose server folder")).Disabled(a.busy).Clicked() {
				a.pickDirectory(chosen.ID, "path")
			}
			if a.page == "smb" && chosen.ID == "smb.share" && ui.Button(c, a.tr("选择共享目录", "Choose shared folder")).Disabled(a.busy).Clicked() {
				a.pickDirectory(chosen.ID, "path")
			}
		})
	})
}

func (a *application) field(c *ui.Context, op action, key string) {
	label := a.tr(fieldNames[key][0], fieldNames[key][1])
	value := a.value(op.ID, key)
	choices := a.choices[key]
	if key == "distribution" && (op.ID == "wsl.import" || op.ID == "wsl.install-distro") {
		choices = nil
	}
	options := []string{}
	switch key {
	case "protocol":
		options = []string{"TCP", "UDP", "ANY"}
	case "direction":
		options = []string{"in", "out", "Both"}
	case "mode":
		options = []string{"DHCP", "Static"}
		if op.ID == "network.reset" {
			options = []string{"FlushDns", "AdapterDhcp", "SystemStack"}
			ui.Text(c, a.tr("FlushDns = 清理缓存；AdapterDhcp = 所选网卡 DHCP/DNS；SystemStack = Winsock/TCP/IP（需重启）", "FlushDns = cache; AdapterDhcp = selected adapter DHCP/DNS; SystemStack = Winsock/TCP/IP (restart required)"))
		}
	case "type":
		options = []string{"Internal", "Private", "External"}
	case "direct", "smb1", "share", "enable", "cors":
		flag := *value == "true"
		if ui.Checkbox(c.Key(op.ID+key), &flag, label).Disabled(a.busy).Changed() {
			*value = fmt.Sprint(flag)
		}
		return
	case "version":
		options = []string{"1", "2"}
	}
	ui.Column(c).Gap(6).Children(func() {
		ui.Text(c, label).FontSize(13).TextColor(c.Theme().TextMuted)
		if len(choices) > 0 {
			options = []string{}
			chosen := ""
			for _, ch := range choices {
				options = append(options, ch.Label)
				if ch.Value == *value {
					chosen = ch.Label
				}
			}
			if ui.Select(c.Key(op.ID+key+"choices"), &chosen, options).Label(label).Disabled(a.busy).Changed() {
				for _, ch := range choices {
					if ch.Label == chosen {
						*value = ch.Value
					}
				}
			}
			ui.Text(c, *value).FontSize(11).TextColor(c.Theme().TextMuted)
		} else if len(options) > 0 {
			ui.Select(c.Key(op.ID+key), value, options).Label(label).Disabled(a.busy)
		} else if key == "json" || key == "connection" {
			ui.TextArea(c.Key(op.ID+key), value).Height(140).Label(label).Disabled(a.busy)
		} else {
			ui.TextInput(c.Key(op.ID+key), value).Label(label).Disabled(a.busy)
		}
	})
}

func (a *application) request(op action) {
	args := map[string]string{}
	for _, key := range actionFields(op) {
		args[key] = *a.value(op.ID, key)
	}
	if op.ID == "network.reset" && args["mode"] != "AdapterDhcp" {
		args["adapterId"] = ""
	}
	if op.Read {
		a.perform(op, args, false)
		return
	}
	a.pending = pendingOperation{op, args, a.label(op) + "\n\n" + a.warning(a.page) + "\n\n" + formatArgs(args)}
	a.confirming = true
}

func formatArgs(args map[string]string) string {
	data, _ := json.MarshalIndent(args, "", "  ")
	return string(data)
}

func (a *application) perform(op action, args map[string]string, confirmed bool) {
	if a.busy {
		return
	}
	if !op.Read && !confirmed {
		return
	}
	if a.engine == nil {
		a.failed = true
		a.status = a.tr("共享服务引擎不可用。请使用完整 Windows 安装包。", "Shared engine unavailable. Use the complete Windows package.")
		return
	}
	a.busy = true
	a.status = ""
	pageID := a.page
	language := a.prefs.Language
	go func() {
		data, err := a.engine.call(op.ID, args, confirmed, language)
		a.win.Update(func() {
			a.busy = false
			a.failed = err != nil
			if err != nil {
				a.status = err.Error()
				return
			}
			a.results[pageID] = formatResult(data)
			a.failed = resultFailed(data)
			a.status = a.tr("操作已完成，请检查结果。", "Operation completed. Review the results.")
			if a.failed {
				a.status = a.tr("部分操作失败或未完成，请检查逐项结果。", "Some operations failed or were incomplete. Review every result.")
			}
			a.loadChoices(op.ID, data)
			if op.ID == "dashboard" {
				log.Print("MyGo ready: dashboard")
			}
		})
	}()
}

func formatResult(data json.RawMessage) string {
	var text string
	if json.Unmarshal(data, &text) == nil {
		return text
	}
	var pretty bytes.Buffer
	if json.Indent(&pretty, data, "", "  ") == nil {
		return pretty.String()
	}
	return string(data)
}

func resultFailed(data json.RawMessage) bool {
	var success bool
	if json.Unmarshal(data, &success) == nil && string(data) != "null" {
		return !success
	}
	var result struct {
		Success *bool `json:"success"`
	}
	return json.Unmarshal(data, &result) == nil && result.Success != nil && !*result.Success
}

func (a *application) loadChoices(op string, data json.RawMessage) {
	var rows []map[string]any
	text := func(row map[string]any, key string) string { value, _ := row[key].(string); return value }
	if op == "dashboard" {
		_ = json.Unmarshal(data, &a.stats)
	}
	if op == "network.list" && json.Unmarshal(data, &rows) == nil {
		a.choices["adapter"] = nil
		a.choices["adapterId"] = nil
		for _, row := range rows {
			name := text(row, "name")
			id := text(row, "interfaceGuid")
			label := name + " · " + text(row, "status")
			a.choices["adapter"] = append(a.choices["adapter"], choice{label, name})
			a.choices["adapterId"] = append(a.choices["adapterId"], choice{label, id})
		}
	}
	if op == "hyperv.status" {
		var status struct {
			VirtualAdapters  []struct{ ID, Name, Status string }
			PhysicalAdapters []struct{ ID, Name, Status string }
			Switches         []struct{ ID, Name string }
		}
		if json.Unmarshal(data, &status) == nil {
			for _, key := range []string{"ids", "physicalId", "id"} {
				a.choices[key] = nil
			}
			all := []string{}
			for _, row := range status.VirtualAdapters {
				a.choices["ids"] = append(a.choices["ids"], choice{row.Name + " · " + row.Status, row.ID})
				all = append(all, row.ID)
			}
			if len(all) > 0 {
				a.choices["ids"] = append(a.choices["ids"], choice{a.tr("全部虚拟网卡", "All virtual adapters"), strings.Join(all, ",")})
			}
			for _, row := range status.PhysicalAdapters {
				a.choices["physicalId"] = append(a.choices["physicalId"], choice{row.Name + " · " + row.Status, row.ID})
			}
			for _, row := range status.Switches {
				if strings.HasPrefix(strings.ToLower(row.Name), "winxinai-") {
					a.choices["id"] = append(a.choices["id"], choice{row.Name, row.ID})
				}
			}
		}
	}
	if op == "wsl.status" {
		var status struct{ Distributions []struct{ Name string } }
		if json.Unmarshal(data, &status) == nil {
			a.choices["distribution"] = nil
			for _, row := range status.Distributions {
				a.choices["distribution"] = append(a.choices["distribution"], choice{row.Name, row.Name})
			}
		}
	}
}

func (a *application) pickDirectory(op, key string) {
	if a.win == nil {
		return
	}
	a.busy = true
	go func() {
		paths, err := mygo.Dialog.Open(mygo.OpenDialogOptions{Parent: a.win, Directory: true})
		a.win.Update(func() {
			a.busy = false
			if err != nil {
				a.status = err.Error()
				a.failed = true
			} else if len(paths) > 0 {
				*a.value(op, key) = paths[0]
			}
		})
	}()
}

func (a *application) saveResult(text string) {
	if a.win == nil {
		return
	}
	a.busy = true
	go func() {
		path, err := mygo.Dialog.Save(mygo.SaveDialogOptions{Parent: a.win, DefaultPath: "WinXinAi-result.json"})
		if err == nil && path != "" {
			err = os.WriteFile(path, []byte(text), 0600)
		}
		a.win.Update(func() {
			a.busy = false
			if err != nil {
				a.status = err.Error()
				a.failed = true
			}
		})
	}()
}

func (a *application) warning(page string) string {
	switch page {
	case "reset":
		return a.tr("先备份网络配置。仅在本机操作，不要通过远程连接重置。恢复网卡会移除所选网卡的手动 IPv4 地址/默认路由并恢复自动 DNS；没有 DHCP 的网络可能断网。Winsock/TCP/IP 重置影响全机、VPN 与虚拟网络，需手动重启。不会自动重启、重置防火墙或删除网卡/交换机。逐项查看结果，备份供手工恢复，不是自动回滚。", "Configuration is backed up first. Use a local session, never a dependent remote connection. Adapter reset removes its manual IPv4 addresses/default route and restores automatic DNS; networks without DHCP may disconnect. Stack reset affects the whole host, VPNs and virtual networking and needs a manual restart. No automatic reboot, firewall reset or adapter/switch deletion. Review every result. Backups support manual recovery, not automatic rollback.")
	case "network":
		return a.tr("修改 IP/DNS 或路由可能立即断网。应用前检查网卡与配置，在本机操作。", "IP/DNS/route changes can immediately disconnect the computer. Verify the adapter and settings in a local session.")
	case "hyperv":
		return a.tr("关闭外部交换机的虚拟网卡可能使主机断网。彻底关闭影响 WSL2、Docker 和虚拟化安全；功能与启动配置需手动重启。仅可删除无虚拟机连接的 WinXinAi- 交换机。先刷新状态，再选择 GUID。", "Disabling an external switch's virtual adapter can disconnect the host. Complete disable affects WSL2, Docker and virtualization-based security; restart manually after feature/boot changes. Only unused WinXinAi- switches can be removed. Refresh status before choosing GUIDs.")
	case "smb":
		return a.tr("高风险：本项目的 share 共享启用无密码 Guest，授予 Everyone 修改权限并修改主机 SMB 身份验证策略。只在可信隔离网络使用专用目录，勿共享私人文件。SMB 1.0 是旧协议。", "HIGH RISK: the share feature enables passwordless Guest, grants Everyone modify access and changes host SMB authentication policy. Use a dedicated folder on a trusted isolated network only. SMB 1.0 is obsolete.")
	case "wsl":
		return a.tr("停止/关闭可能丢失未保存工作；注销会永久删除发行版数据。导入、迁移、VHD、计划任务和端口代理会修改系统配置。操作前备份；不要对不明命令或路径授权。", "Stop/shutdown can lose work; unregister permanently deletes the distribution's data. Import, migration, VHD, scheduled tasks and forwarding change system configuration. Back up first and never authorize unknown commands or paths.")
	case "http":
		return a.tr("监听所有可用网卡，并添加临时防火墙规则。目录文件无需登录即可被访问；仅分享专用目录。Stop/正常退出会清理，崩溃后可能需手工清理。更改语言或主题不会停止服务。", "Listens on all available interfaces and opens a temporary firewall rule. Files are accessible without login; share a dedicated directory only. Stop/normal exit cleans up; a crash can require manual cleanup. Language/theme changes keep the server running.")
	case "connections":
		return a.tr("关闭连接会中断会话；结束进程会丢失未保存工作。仅阻止 PID 不大于 4，不保证其他关键进程安全，请先核对 PID。", "Closing connections interrupts sessions; terminating processes loses unsaved work. Only PIDs up to 4 are blocked, not every critical process. Verify the PID first.")
	case "firewall", "transfer":
		return a.tr("规则修改会影响全机。导入可能部分成功；修改规则会先删除旧规则。请备份并检查每项结果。", "Rule changes affect the whole host. Import can partially succeed; modification removes the old rule first. Back up and inspect every result.")
	default:
		return ""
	}
}
