package main

import "strings"

type action struct {
	ID, ZH, EN, Fields string
	Read               bool
}

type page struct{ ID, ZH, EN, DescriptionZH, DescriptionEN string }

var pages = []page{
	{"dashboard", "概览", "Overview", "本机状态与常用工具", "Your computer, at a glance"},
	{"firewall", "防火墙", "Firewall", "查看、添加和管理 Windows 防火墙规则", "Inspect, add and manage Windows Firewall rules"},
	{"network", "网络配置", "Network", "网卡、IPv4、DNS 与默认路由", "Adapters, IPv4, DNS and default routes"},
	{"reset", "网络重置", "Network reset", "先备份，再有针对性地修复网络", "Back up first. Reset only what you need."},
	{"hyperv", "Hyper-V", "Hyper-V", "虚拟化平台、虚拟网卡与交换机", "Virtualization, virtual adapters and switches"},
	{"smb", "SMB 共享", "SMB sharing", "SMB 功能和文件夹共享", "SMB features and folder sharing"},
	{"wsl", "WSL 管理", "WSL manager", "发行版、存储、代理、端口转发与 USB", "Distributions, storage, proxies, forwarding and USB"},
	{"http", "临时 HTTP", "Temporary HTTP", "分享专用目录中的静态文件，不提供身份验证", "Share a dedicated directory. No authentication."},
	{"connections", "连接监控", "Connections", "连接、端口和所属进程", "Connections, ports and their processes"},
	{"transfer", "规则导入导出", "Rule transfer", "与原版共用 JSON 规则格式", "The same JSON rule format as the WinUI edition"},
	{"audit", "操作日志", "Activity log", "检查成功、失败和部分完成的系统操作", "Review successful, failed and partial system operations"},
	{"settings", "设置与关于", "Settings & about", "Built with MyGo · Go 原生界面，共享系统服务引擎", "Built with MyGo · Go native UI, shared system engine"},
}

var actions = map[string][]action{
	"dashboard": {{"dashboard", "刷新概览", "Refresh overview", "", true}},
	"firewall": {
		{"firewall.list", "查看规则", "List rules", "", true},
		{"firewall.query", "查询端口", "Query port", "port", true},
		{"firewall.add", "添加规则", "Add rule", "port protocol direction name application", false},
		{"firewall.modify", "修改规则", "Modify rule", "oldName port protocol direction name", false},
		{"firewall.delete", "删除规则", "Delete rule", "name", false},
	},
	"network": {
		{"network.list", "查看网卡", "List adapters", "", true},
		{"network.read", "读取所选网卡配置", "Read selected adapter", "adapterId", true},
		{"network.apply", "应用 IP/DNS 配置", "Apply IP/DNS settings", "adapterId mode ip prefix gateway dns dns2 metric", false},
	},
	"reset": {{"network.reset", "重置本地网络设置", "Reset local network settings", "mode adapterId", false}},
	"hyperv": {
		{"hyperv.status", "刷新状态", "Refresh status", "", true},
		{"hyperv.enable", "启用 Hyper-V", "Enable Hyper-V", "", false},
		{"hyperv.disable", "仅关闭 Hyper-V 功能", "Disable Hyper-V feature only", "", false},
		{"hyperv.complete", "彻底关闭平台与启动加载", "Complete platform/boot disable", "", false},
		{"hyperv.adapters", "启停虚拟网卡", "Enable/disable virtual adapters", "ids enable", false},
		{"hyperv.switch.create", "创建交换机", "Create switch", "name type physicalId share", false},
		{"hyperv.switch.remove", "删除本工具的交换机", "Remove tool switch", "id", false},
	},
	"smb": {
		{"smb.status", "刷新 SMB 状态", "Refresh SMB status", "", true},
		{"smb.addresses", "查看访问地址", "Show access addresses", "", true},
		{"smb.features", "设置 SMB 功能（不重启）", "Set SMB features (no restart)", "direct smb1", false},
		{"smb.share", "设置无密码 Guest 共享", "Set passwordless Guest share", "path", false},
		{"smb.remove", "移除共享并恢复身份验证", "Remove share / restore authentication", "", false},
	},
	"wsl": {
		{"wsl.status", "刷新发行版", "Refresh distributions", "", true},
		{"wsl.online", "可安装的发行版", "Available distributions", "", true},
		{"wsl.version", "版本信息", "Version information", "", true},
		{"wsl.install", "安装 WSL", "Install WSL", "", false},
		{"wsl.install-distro", "安装发行版", "Install distribution", "distribution", false},
		{"wsl.update", "更新 WSL", "Update WSL", "", false},
		{"wsl.start", "启动发行版", "Start distribution", "distribution", false},
		{"wsl.stop", "停止发行版", "Stop distribution", "distribution", false},
		{"wsl.default", "设为默认", "Set as default", "distribution", false},
		{"wsl.shutdown", "关闭全部 WSL", "Shut down all WSL", "", false},
		{"wsl.export", "导出备份", "Export backup", "distribution path", false},
		{"wsl.import", "导入发行版", "Import distribution", "distribution target path version", false},
		{"wsl.move", "迁移存储", "Move storage", "distribution path", false},
		{"wsl.disk", "查询磁盘占用", "Inspect disk usage", "distribution", true},
		{"wsl.terminal", "打开终端", "Open terminal", "distribution", false},
		{"wsl.explorer", "打开文件", "Open files", "distribution", false},
		{"wsl.code", "打开 VS Code", "Open VS Code", "distribution", false},
		{"wsl.autostart", "设置发行版自启", "Distribution autostart", "distribution enable", false},
		{"wsl.schedule", "创建计划命令", "Schedule command", "distribution name command schedule time", false},
		{"wsl.unschedule", "移除计划命令", "Remove scheduled command", "name", false},
		{"wsl.port.add", "添加端口转发", "Add port forwarding", "listenPort address port", false},
		{"wsl.port.remove", "移除端口转发", "Remove port forwarding", "listenPort", false},
		{"wsl.proxy", "设置 WinHTTP 代理（空值为清除）", "Set WinHTTP proxy (empty resets)", "proxy", false},
		{"wsl.mount", "挂载 VHD", "Mount VHD", "path", false},
		{"wsl.unmount", "卸载 VHD", "Unmount VHD", "path", false},
		{"wsl.usb", "USB 设备", "USB devices", "", true},
		{"wsl.usb.bind", "绑定 USB", "Bind USB", "busId", false},
		{"wsl.usb.attach", "连接 USB 到 WSL", "Attach USB to WSL", "busId distribution", false},
		{"wsl.usb.detach", "断开 USB", "Detach USB", "busId", false},
		{"wsl.unregister", "注销并删除发行版", "Unregister / delete distribution", "distribution", false},
	},
	"http": {
		{"http.status", "服务状态与访问地址", "Server status and URLs", "", true},
		{"http.discover", "检测本机 HTTP 服务", "Discover local HTTP services", "", true},
		{"http.start", "启动服务（允许外部访问）", "Start server (external access)", "path port cors", false},
		{"http.restart", "重启服务", "Restart server", "path port cors", false},
		{"http.stop", "停止并清理防火墙规则", "Stop / clean firewall rule", "", false},
	},
	"connections": {
		{"connections.list", "刷新连接", "Refresh connections", "", true},
		{"connections.close", "关闭 TCP 连接", "Close TCP connection", "connection", false},
		{"connections.terminate", "结束进程", "Terminate process", "pid", false},
	},
	"transfer": {{"firewall.export", "导出规则", "Export rules", "", true}, {"firewall.import", "导入规则", "Import rules", "json", false}},
	"audit":    {{"audit.list", "读取日志", "Read activity log", "", true}, {"audit.clear", "清理日志", "Clear activity log", "", false}},
}

var fieldNames = map[string][2]string{
	"port": {"端口 (1–65535)", "Port (1–65535)"}, "protocol": {"协议", "Protocol"}, "direction": {"方向：in / out / Both", "Direction: in / out / Both"},
	"name": {"名称", "Name"}, "oldName": {"原规则名称", "Original rule name"}, "application": {"程序完整路径（可选）", "Application path (optional)"},
	"adapter": {"网卡名称", "Adapter name"}, "adapterId": {"网卡 GUID（从网络配置列表选择）", "Adapter GUID (choose from network list)"}, "mode": {"模式", "Mode"},
	"ip": {"IPv4 地址", "IPv4 address"}, "prefix": {"前缀长度", "Prefix length"}, "gateway": {"网关（可选）", "Gateway (optional)"}, "dns": {"首选 DNS", "Primary DNS"}, "dns2": {"备用 DNS", "Secondary DNS"}, "metric": {"路由跃点", "Route metric"},
	"ids": {"虚拟网卡 GUID（逗号分隔）", "Virtual adapter GUIDs (comma separated)"}, "enable": {"启用", "Enable"}, "type": {"交换机类型", "Switch type"}, "physicalId": {"物理网卡 GUID", "Physical adapter GUID"}, "share": {"允许主机共享物理网卡", "Allow management OS sharing"}, "id": {"交换机 GUID", "Switch GUID"},
	"path": {"文件／文件夹完整路径", "Full file / directory path"}, "target": {"目标安装目录", "Target install directory"}, "json": {"规则 JSON", "Rules JSON"}, "direct": {"启用 SMB Direct", "Enable SMB Direct"}, "smb1": {"启用旧版 SMB 1.0（高风险）", "Enable legacy SMB 1.0 (high risk)"},
	"distribution": {"发行版名称", "Distribution name"}, "version": {"WSL 版本", "WSL version"}, "command": {"Linux 命令", "Linux command"}, "schedule": {"周期：DAILY / WEEKLY / ONCE", "Schedule: DAILY / WEEKLY / ONCE"}, "time": {"时间 HH:mm", "Time HH:mm"}, "listenPort": {"监听端口", "Listening port"}, "address": {"目标 IPv4 地址", "Target IPv4 address"}, "proxy": {"代理地址", "Proxy address"}, "busId": {"USB BUSID", "USB BUSID"}, "cors": {"允许任意网站跨域读取", "Allow cross-origin reads"}, "pid": {"进程 PID", "Process PID"}, "connection": {"连接对象 JSON（从查询结果复制）", "Connection JSON (copy from query result)"},
}

func defaultValue(op, key string) string {
	if key == "mode" && op == "network.reset" {
		return "FlushDns"
	}
	return map[string]string{"port": "8980", "protocol": "TCP", "direction": "in", "mode": "DHCP", "prefix": "24", "metric": "25", "type": "Internal", "share": "true", "version": "2", "distribution": "Ubuntu", "schedule": "DAILY", "time": "09:00"}[key]
}

func actionByID(id string) (action, bool) {
	for _, group := range actions {
		for _, a := range group {
			if a.ID == id {
				return a, true
			}
		}
	}
	return action{}, false
}

func actionFields(a action) []string { return strings.Fields(a.Fields) }
