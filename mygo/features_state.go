package main

import (
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"github.com/egoist/mygo/ui"
	"strconv"
	"strings"
)

type engineService interface {
	call(string, map[string]string, bool, string) (json.RawMessage, error)
	close() error
}

type featureUI struct {
	diagnostics                             bool
	ruleFilter, connectionFilter, direction string
	rulePage, connectionPage, logPage       int
	networkID, networkConfigID              string
	wslTab                                  int
	expanded                                map[string]*bool
}

type firewallRule struct {
	Name                                    string
	Direction                               string `json:"Dir"`
	Protocol                                string `json:"Proto"`
	LocalPort, RemotePort, Profile, Enabled string
}

func (r firewallRule) port() string {
	if r.Direction == "Outbound" && r.RemotePort != "" && !strings.EqualFold(r.RemotePort, "Any") {
		return r.RemotePort
	}
	return r.LocalPort
}
func (r firewallRule) protocol() string {
	switch r.Protocol {
	case "6":
		return "TCP"
	case "17":
		return "UDP"
	case "256", "Any":
		return "ANY"
	}
	return strings.ToUpper(r.Protocol)
}

type networkAdapter struct {
	Name, Status, MacAddress, InterfaceGuid string
	InterfaceIndex                          int
}
type networkConfig struct {
	InterfaceAlias     string
	InterfaceIndex     int
	DhcpEnabled        bool
	IPv4Address        string
	PrefixLength       int
	Gateway            string
	DnsServers         []string
	DefaultRouteMetric int
}
type hyperAdapter struct{ ID, Name, Status string }
type hyperSwitch struct {
	ID, Name, SwitchType string
	AllowManagementOS    bool
}
type hyperStatus struct {
	Features                                  []struct{ Name, State string }
	VirtualAdapters, PhysicalAdapters         []hyperAdapter
	Switches                                  []hyperSwitch
	HypervisorPresent, ManagementAvailable    bool
	BootLaunchType, NetworkError, SwitchError string
}
type smbStatus struct {
	SmbDirectEnabled, Smb1Enabled, ShareExists bool
	ShareName, SharePath                       string
}
type smbAddress struct{ WindowsPath, MacUrl string }
type distribution struct {
	Name, State string
	Version     int
	IsDefault   bool
}
type wslStatus struct {
	IsInstalled   bool
	Distributions []distribution
}
type connection struct {
	Protocol, LocalAddress string
	LocalPort              int
	RemoteAddress          string
	RemotePort             int
	State                  string
	ProcessId              int
	ProcessName            string
}
type httpStatus struct {
	IsRunning, NeedsFirewallCleanup bool
	RequestCount                    int64
	Active                          *struct {
		Port      int
		Directory string
		AllowCors bool
	}
	URLs []string
}
type httpScan struct {
	Servers []struct {
		URL              string
		Port, StatusCode int
		ProcessId        *int
		ProcessName      string
	}
	IsPartial bool
}
type auditEntry struct {
	Timestamp, Action, Details string
	Success                    bool
}
type resetResult struct {
	BackupPath               string
	RestartRequired, Success bool
	Steps                    []struct {
		Name, Output string
		Success      bool
	}
}
type operationResult struct {
	Success                   bool
	Message, ErrorMessage     string
	SuccessCount, FailedCount int
}
type ruleDocument struct {
	FormatVersion int
	Rules         []firewallRule
}

func readData[T any](a *application, id string) (T, bool) {
	var value T
	data, ok := a.data[id]
	if !ok || a.readErrors[id] != "" {
		return value, false
	}
	err := json.Unmarshal(data, &value)
	return value, err == nil
}

func pageReads(page string) []string {
	switch page {
	case "dashboard":
		return []string{"dashboard"}
	case "firewall":
		return []string{"firewall.list"}
	case "network", "reset":
		return []string{"network.list"}
	case "hyperv":
		return []string{"hyperv.status"}
	case "smb":
		return []string{"smb.status", "smb.addresses"}
	case "wsl":
		return []string{"wsl.status"}
	case "http":
		return []string{"http.status"}
	case "connections":
		return []string{"connections.list"}
	case "audit":
		return []string{"audit.list"}
	}
	return nil
}

// Read batches finish on the UI thread. Responses are keyed by operation, never by
// whichever page happens to be open when an asynchronous request completes.
func (a *application) refreshPage() {
	if a.busy || a.engine == nil || (a.win == nil && a.postUpdate == nil) {
		return
	}
	ids := pageReads(a.page)
	if len(ids) == 0 {
		return
	}
	pageID, language := a.page, a.prefs.Language
	a.busy = true
	type response struct {
		id   string
		data json.RawMessage
		err  error
	}
	go func() {
		rows := []response{}
		for _, id := range ids {
			data, err := a.engine.call(id, nil, false, language)
			rows = append(rows, response{id: id, data: data, err: err})
		}
		a.update(func() {
			a.busy = false
			for _, row := range rows {
				if row.err != nil {
					a.readErrors[row.id] = row.err.Error()
					continue
				}
				if err := a.acceptData(row.id, row.data, nil); err != nil {
					a.readErrors[row.id] = err.Error()
				}
			}
			if pageID == "network" && a.page == pageID && a.readErrors["network.list"] == "" && a.uiState.networkID != "" {
				a.loadNetworkConfig()
			}
		})
	}()
}

func (a *application) loadNetworkConfig() {
	args := map[string]string{"adapterId": a.uiState.networkID}
	language := a.prefs.Language
	a.busy = true
	go func() {
		data, err := a.engine.call("network.read", args, false, language)
		a.update(func() {
			a.busy = false
			if err == nil {
				err = a.acceptData("network.read", data, args)
			}
			if err != nil {
				a.readErrors["network.read"] = err.Error()
			}
		})
	}()
}

func (a *application) update(fn func()) {
	if a.postUpdate != nil {
		a.postUpdate(fn)
		return
	}
	a.win.Update(fn)
}

func (a *application) runRead(id string, args map[string]string) {
	op, ok := actionByID(id)
	if ok && op.Read {
		a.perform(op, args, false)
	}
}
func (a *application) requestArgs(id string, args map[string]string) {
	op, ok := actionByID(id)
	if !ok || a.busy || a.confirming {
		return
	}
	// Freeze row identity and arguments; later filtering or draft changes cannot retarget confirmation.
	frozen := map[string]string{}
	for k, v := range args {
		frozen[k] = v
	}
	if op.Read {
		a.perform(op, frozen, false)
		return
	}
	page := a.page
	a.pending = pendingOperation{op, frozen, a.label(op) + "\n\n" + a.warning(page) + "\n\n" + confirmationArgs(frozen)}
	a.confirming = true
}

func confirmationArgs(args map[string]string) string {
	summary := map[string]string{}
	for key, value := range args {
		if key == "json" || len(value) > 1024 {
			summary[key] = fmt.Sprintf("%d bytes; SHA-256 %x", len(value), sha256.Sum256([]byte(value)))
			if key == "json" {
				if doc, err := parseRulePreview(value); err == nil {
					summary[key] = fmt.Sprintf("%d rules; %s", len(doc.Rules), summary[key])
				}
			}
		} else {
			summary[key] = value
		}
	}
	return formatArgs(summary)
}

func (a *application) acceptData(id string, data json.RawMessage, args map[string]string) error {
	var target any
	switch id {
	case "dashboard":
		target = &a.stats
	case "firewall.list", "firewall.query":
		target = &[]firewallRule{}
	case "network.list":
		target = &[]networkAdapter{}
	case "network.read":
		target = &networkConfig{}
	case "hyperv.status":
		target = &hyperStatus{}
	case "smb.status":
		target = &smbStatus{}
	case "smb.addresses":
		target = &[]smbAddress{}
	case "wsl.status":
		target = &wslStatus{}
	case "http.status", "http.start", "http.restart", "http.stop":
		target = &httpStatus{}
	case "connections.list":
		target = &[]connection{}
	case "http.discover":
		target = &httpScan{}
	case "audit.list":
		target = &[]auditEntry{}
	}
	if target != nil {
		if strings.TrimSpace(string(data)) == "null" {
			return fmt.Errorf("%s: empty response", id)
		}
		if err := json.Unmarshal(data, target); err != nil {
			return fmt.Errorf("%s: %w", id, err)
		}
	}
	if id == "network.read" && args["adapterId"] != a.uiState.networkID {
		return nil
	}
	_, previouslyLoaded := a.data[id]
	a.data[id] = append(json.RawMessage(nil), data...)
	delete(a.readErrors, id)
	a.loadChoices(id, data)
	switch id {
	case "network.list":
		adapters, _ := readData[[]networkAdapter](a, id)
		found := false
		for _, row := range adapters {
			found = found || row.InterfaceGuid == a.uiState.networkID
		}
		if !found {
			a.uiState.networkID, a.uiState.networkConfigID = "", ""
			for _, row := range adapters {
				if row.Status == "Up" {
					a.uiState.networkID = row.InterfaceGuid
					break
				}
			}
			if a.uiState.networkID == "" && len(adapters) > 0 {
				a.uiState.networkID = adapters[0].InterfaceGuid
			}
		}
	case "network.read":
		// Refresh current facts without overwriting a same-adapter, unsubmitted draft.
		if a.uiState.networkConfigID == args["adapterId"] {
			return nil
		}
		a.uiState.networkConfigID = args["adapterId"]
		config, _ := readData[networkConfig](a, id)
		a.setField("network.apply", "adapterId", args["adapterId"])
		mode := "Static"
		if config.DhcpEnabled {
			mode = "DHCP"
		}
		values := map[string]string{"mode": mode, "ip": config.IPv4Address, "prefix": strconv.Itoa(config.PrefixLength), "gateway": config.Gateway, "metric": strconv.Itoa(config.DefaultRouteMetric), "dns": "", "dns2": ""}
		if len(config.DnsServers) > 0 {
			values["dns"] = config.DnsServers[0]
		}
		if len(config.DnsServers) > 1 {
			values["dns2"] = config.DnsServers[1]
		}
		for key, val := range values {
			a.setField("network.apply", key, val)
		}
	case "smb.status":
		if previouslyLoaded {
			return nil
		}
		status, _ := readData[smbStatus](a, id)
		a.setField("smb.features", "direct", strconv.FormatBool(status.SmbDirectEnabled))
		a.setField("smb.features", "smb1", strconv.FormatBool(status.Smb1Enabled))
	case "http.status":
		status, _ := readData[httpStatus](a, id)
		if status.Active != nil {
			for key, value := range map[string]string{"path": status.Active.Directory, "port": strconv.Itoa(status.Active.Port), "cors": strconv.FormatBool(status.Active.AllowCors)} {
				if a.fields["http.restart:"+key] == nil {
					a.setField("http.restart", key, value)
				}
			}
		}
	}
	return nil
}
func (a *application) setField(op, key, value string) { *a.value(op, key) = value }
func (a *application) expanded(id string) *bool {
	if a.uiState.expanded == nil {
		a.uiState.expanded = map[string]*bool{}
	}
	if a.uiState.expanded[id] == nil {
		v := false
		a.uiState.expanded[id] = &v
	}
	return a.uiState.expanded[id]
}

func (a *application) pageScroll(page string) *ui.ScrollState {
	if a.scrolls == nil {
		a.scrolls = map[string]*ui.ScrollState{}
	}
	if a.scrolls[page] == nil {
		a.scrolls[page] = &ui.ScrollState{}
	}
	return a.scrolls[page]
}
