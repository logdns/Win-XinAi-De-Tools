package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/egoist/mygo/ui"
)

func testApplication(t *testing.T) *application {
	t.Helper()
	t.Setenv("LOCALAPPDATA", t.TempDir())
	a := newApplication()
	a.fixture = true
	return a
}

func TestEveryNativePageLanguageThemeAndSize(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	count := 0
	for _, language := range []string{"zh-CN", "en-US"} {
		for _, theme := range []string{"Light", "Dark"} {
			for _, size := range [][2]int{{480, 640}, {800, 600}, {1400, 900}} {
				for _, page := range pages {
					a.page = page.ID
					a.prefs.Language = language
					a.prefs.Theme = theme
					tester := ui.NewTester(a.view, size[0], size[1])
					if !tester.HasText(a.tr(page.ZH, page.EN)) {
						t.Fatalf("missing native title: %s %s %s", page.ID, language, theme)
					}
					count++
				}
			}
		}
	}
	if count != 144 {
		t.Fatalf("unexpected coverage count: %d", count)
	}
}

func TestEveryOperationHasDedicatedForm(t *testing.T) {
	a := testApplication(t)
	a.prefs.Language = "en-US"
	for page, group := range actions {
		a.page = page
		for _, op := range group {
			tester := ui.NewTester(func(c *ui.Context) { a.form(c, op.ID) }, 1400, 1000)
			if !tester.HasText(op.EN) {
				t.Fatalf("missing operation form: %s", op.ID)
			}
			for _, key := range actionFields(op) {
				if _, ok := fieldNames[key]; !ok {
					t.Fatalf("missing field %s", key)
				}
			}
		}
	}
}

func TestNetworkResetNeedsExplicitCancelDefaultConfirmation(t *testing.T) {
	a := testApplication(t)
	a.page = "reset"
	a.prefs.Language = "en-US"
	tt := ui.NewTester(a.view, 1400, 1000)
	if err := tt.Click("Run: Flush DNS cache"); err != nil {
		t.Fatal(err)
	}
	if !a.confirming || a.busy {
		t.Fatal("reset did not open confirmation before touching engine")
	}
	tt.Key(0, ui.KeyEnter)
	if a.confirming || a.status != "" {
		t.Fatal("Enter did not cancel safely")
	}
}

func TestDraftRetentionAndBackHistory(t *testing.T) {
	a := testApplication(t)
	*a.value("hyperv.switch.create", "name") = "unsaved"
	a.navigate("hyperv")
	a.navigate("reset")
	if len(a.history) != 2 || *a.value("hyperv.switch.create", "name") != "unsaved" {
		t.Fatal("draft/history lost")
	}
	a.confirming = true
	a.navigate("smb")
	if a.page != "reset" {
		t.Fatal("navigation escaped a modal")
	}
}

func TestPreferencesAreSeparateFromWinUI(t *testing.T) {
	a := testApplication(t)
	a.fixture = false
	a.prefs.Language = "en-US"
	a.prefs.Theme = "Dark"
	a.savePreferences()
	if _, err := os.Stat(filepath.Join(os.Getenv("LOCALAPPDATA"), "Win-XinAi-De-Tools-MyGo", "preferences.json")); err != nil {
		t.Fatal(err)
	}
	b := newApplication()
	if b.prefs != a.prefs {
		t.Fatal("preferences not restored")
	}
	if _, err := os.Stat(filepath.Join(os.Getenv("LOCALAPPDATA"), "Win-XinAi-De-Tools", "ui-preferences.json")); !os.IsNotExist(err) {
		t.Fatal("WinUI preferences touched")
	}
}

func TestAllDestructiveOperationsAreConfirmationScoped(t *testing.T) {
	for _, id := range []string{"network.reset", "network.apply", "wsl.unregister", "hyperv.complete", "smb.share", "connections.terminate", "firewall.import", "http.start"} {
		op, exists := actionByID(id)
		if !exists || op.Read {
			t.Fatalf("unsafe catalog declaration: %s", id)
		}
	}
}

func TestResetConfirmationRemainsReachableAtMinimumSize(t *testing.T) {
	for _, language := range []string{"zh-CN", "en-US"} {
		a := testApplication(t)
		a.page = "reset"
		a.prefs.Language = language
		op, _ := actionByID("network.reset")
		a.request(op)
		tt := ui.NewTester(a.view, 480, 480)
		box, ok := tt.Find(a.tr("取消", "Cancel"))
		if !ok || box.X < 0 || box.Y < 0 || box.X+box.W > 481 || box.Y+box.H > 481 {
			t.Fatalf("confirmation cancel is clipped: %s %+v", language, box)
		}
		tt.Key(0, ui.KeyEnter)
		if a.confirming || a.busy {
			t.Fatal("minimum-size default did not cancel")
		}
	}
}

func TestPartialAndFalseOperationResultsRemainFailures(t *testing.T) {
	for _, data := range []string{`false`, `{"success":false,"failedCount":1}`} {
		if !resultFailed([]byte(data)) {
			t.Fatalf("failure hidden: %s", data)
		}
	}
	for _, data := range []string{`true`, `{"success":true}`, `{"completed":true}`, `[]`, `null`} {
		if resultFailed([]byte(data)) {
			t.Fatalf("unexpected failure: %s", data)
		}
	}
}

func TestSmokeAndNormalOperationsUseSameReadableResult(t *testing.T) {
	if got := formatResult([]byte(`{"success":false,"steps":["one"]}`)); !strings.Contains(got, "\n  \"success\"") {
		t.Fatal("JSON not formatted")
	}
	if got := formatResult([]byte(`"plain text"`)); got != "plain text" {
		t.Fatal("string response remained JSON-quoted")
	}
}

func TestWindowsSDKVersionMatchesOnlyCurrentRelease(t *testing.T) {
	for _, value := range []string{version, version + ".0"} {
		if !matchesReleaseVersion(value) {
			t.Fatalf("valid SDK version rejected: %s", value)
		}
	}
	for _, value := range []string{"", "1.10.1", version + ".1", "1.11.2.0"} {
		if matchesReleaseVersion(value) {
			t.Fatalf("wrong SDK version accepted: %s", value)
		}
	}
}

// Representative populated engine contracts, not title-only empty-page snapshots.
var featureFixtures = map[string]string{
	"dashboard":        `{"rules":12,"inbound":8,"outbound":4}`,
	"firewall.list":    `[{"Name":"SSH fixture","Dir":"Inbound","Proto":"TCP","LocalPort":"22","RemotePort":"Any","Enabled":"True","Profile":"Private"}]`,
	"network.list":     `[{"name":"Ethernet fixture","status":"Up","macAddress":"00-11-22-33-44-55","interfaceGuid":"11111111-1111-1111-1111-111111111111"}]`,
	"network.read":     `{"interfaceAlias":"Ethernet fixture","dhcpEnabled":false,"iPv4Address":"192.0.2.20","prefixLength":24,"gateway":"192.0.2.1","dnsServers":["192.0.2.53","192.0.2.54"],"defaultRouteMetric":25}`,
	"hyperv.status":    `{"features":[{"name":"Microsoft-Hyper-V-All","state":"Enabled"}],"virtualAdapters":[{"id":"22222222-2222-2222-2222-222222222222","name":"vEthernet fixture","status":"Up"}],"physicalAdapters":[],"switches":[{"id":"33333333-3333-3333-3333-333333333333","name":"WinXinAi-Fixture","switchType":"Internal","allowManagementOS":true},{"id":"44444444-4444-4444-4444-444444444444","name":"Default Switch","switchType":"Internal"}],"hypervisorPresent":true,"bootLaunchType":"Auto","managementAvailable":true}`,
	"smb.status":       `{"smbDirectEnabled":true,"smb1Enabled":false,"shareExists":true,"shareName":"share","sharePath":"C:/Share fixture"}`,
	"smb.addresses":    `[{"windowsPath":"fixture-share-address","macUrl":"smb://192.0.2.20/share"}]`,
	"wsl.status":       `{"isInstalled":true,"distributions":[{"name":"Ubuntu fixture","state":"Running","version":2,"isDefault":true}]}`,
	"http.status":      `{"isRunning":true,"active":{"port":8980,"directory":"C:/HTTP fixture","allowCors":false},"requestCount":7,"urls":["http://192.0.2.20:8980/"]}`,
	"connections.list": `[{"protocol":"TCP","localAddress":"192.0.2.20","localPort":50000,"remoteAddress":"192.0.2.30","remotePort":443,"state":"Established","processId":1234,"processName":"Browser fixture"}]`,
	"audit.list":       `[{"timestamp":"2026-10-17T10:00:00+08:00","action":"Fixture audit action","details":"Fixture failure detail","success":false}]`,
}

func populateFeatures(t *testing.T, a *application) {
	t.Helper()
	// Adapter list must arrive before its configuration.
	for id, data := range featureFixtures {
		if id == "network.read" {
			continue
		}
		if err := a.acceptData(id, json.RawMessage(data), nil); err != nil {
			t.Fatal(err)
		}
	}
	if err := a.acceptData("network.read", json.RawMessage(featureFixtures["network.read"]), map[string]string{"adapterId": a.uiState.networkID}); err != nil {
		t.Fatal(err)
	}
}
func TestPopulatedFeaturesAreVisibleWithoutAnExecutionConsole(t *testing.T) {
	for _, size := range [][2]int{{480, 640}, {800, 600}, {1400, 900}} {
		for _, language := range []string{"zh-CN", "en-US"} {
			a := testApplication(t)
			populateFeatures(t, a)
			a.prefs.Language = language
			for page, needle := range map[string]string{"firewall": "SSH fixture", "network": "Ethernet fixture", "hyperv": "vEthernet fixture", "smb": "C:/Share fixture", "wsl": "Ubuntu fixture", "http": "C:/HTTP fixture", "connections": "Browser fixture", "audit": "Fixture audit action"} {
				a.page = page
				a.results[page] = "RAW DIAGNOSTIC FIXTURE"
				a.status = a.tr("操作已完成。", "Operation completed.")
				tt := ui.NewTester(a.view, size[0], size[1])
				if !tt.HasText(needle) {
					t.Fatalf("%s missing real content at %v %s: %s", page, size, language, needle)
				}
				if tt.HasText(a.tr("执行所选操作", "Run selected operation")) {
					t.Fatalf("generic console remains on %s", page)
				}
				if tt.HasText(a.tr("操作结果", "Operation results")) {
					t.Fatalf("raw result expanded by default on %s", page)
				}
				if tt.HasText("RAW DIAGNOSTIC FIXTURE") || !tt.HasText(a.status) {
					t.Fatal("diagnostics are expanded or operation status is hidden")
				}
			}
		}
	}
}
func TestNetworkConfigPopulatesEditableFieldsAndDoesNotRetarget(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	for key, want := range map[string]string{"ip": "192.0.2.20", "mode": "Static", "prefix": "24", "gateway": "192.0.2.1", "dns": "192.0.2.53", "dns2": "192.0.2.54", "metric": "25"} {
		if *a.value("network.apply", key) != want {
			t.Fatalf("%s not populated", key)
		}
	}
	old := *a.value("network.apply", "ip")
	a.uiState.networkID = "different-adapter"
	_ = a.acceptData("network.read", []byte(`{"iPv4Address":"198.51.100.20"}`), map[string]string{"adapterId": "old-adapter"})
	if *a.value("network.apply", "ip") != old {
		t.Fatal("stale response overwrote draft")
	}
}
func TestConnectionRowFreezesActualObjectBeforeConfirmation(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "connections"
	a.prefs.Language = "en-US"
	tt := ui.NewTester(a.view, 1400, 1500)
	if err := tt.Click("Close TCP connection"); err != nil {
		t.Fatal(err)
	}
	var target connection
	if !a.confirming || a.pending.Action.ID != "connections.close" || json.Unmarshal([]byte(a.pending.Args["connection"]), &target) != nil || target.ProcessId != 1234 || target.RemotePort != 443 {
		t.Fatal("row did not bind exact connection")
	}
	a.uiState.connectionFilter = "other"
	if a.pending.Args["connection"] == "" {
		t.Fatal("filter changed pending object")
	}
	tt.Key(0, ui.KeyEnter)
	if a.confirming || a.busy || a.status != "" {
		t.Fatal("row confirmation was not cancel-default")
	}
}
func TestHyperVRowControlsNeverTargetPhysicalAdapter(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "hyperv"
	a.prefs.Language = "en-US"
	tt := ui.NewTester(a.view, 1400, 2000)
	if err := tt.Click("Disable adapter"); err != nil {
		t.Fatal(err)
	}
	if !a.confirming || a.pending.Args["ids"] != "22222222-2222-2222-2222-222222222222" || a.pending.Args["enable"] != "false" {
		t.Fatal("wrong adapter target")
	}
	if strings.Contains(a.pending.Args["ids"], "11111111") {
		t.Fatal("physical adapter targeted")
	}
}
func TestResetModesHaveDistinctFrozenScopes(t *testing.T) {
	for mode, label := range map[string]string{"FlushDns": "Flush DNS cache", "SystemStack": "Reset Winsock / TCP/IP", "AdapterDhcp": "Restore adapter DHCP"} {
		a := testApplication(t)
		populateFeatures(t, a)
		a.page = "reset"
		a.prefs.Language = "en-US"
		a.setField("network.reset", "adapterId", "11111111-1111-1111-1111-111111111111")
		tt := ui.NewTester(a.view, 1400, 2000)
		if err := tt.Click("Run: " + label); err != nil {
			t.Fatal(err)
		}
		if !a.confirming || a.pending.Args["mode"] != mode {
			t.Fatalf("wrong reset mode: %s", mode)
		}
		if (a.pending.Args["adapterId"] != "") != (mode == "AdapterDhcp") {
			t.Fatal("adapter leaked into system/cache reset")
		}
	}
}
func TestReadFailuresNeverShowCachedHealthyStatus(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "hyperv"
	a.prefs.Language = "en-US"
	a.readErrors["hyperv.status"] = "Fixture probe failed"
	tt := ui.NewTester(a.view, 1400, 1000)
	if !tt.HasText("Unable to load: Fixture probe failed") || tt.HasText("vEthernet fixture") {
		t.Fatal("cached healthy state exposed after failed refresh")
	}
	a.page = "dashboard"
	a.readErrors["dashboard"] = "fixture failed"
	tt = ui.NewTester(a.view, 1400, 1000)
	if _, ok := tt.Find("12"); ok {
		t.Fatal("stale dashboard counts appear healthy")
	}
}
func TestMalformedResponsesAndImportPreviewAreRejected(t *testing.T) {
	a := testApplication(t)
	for _, data := range []string{`null`, `{"virtualAdapters":"wrong"}`} {
		if a.acceptData("hyperv.status", []byte(data), nil) == nil {
			t.Fatal("malformed status accepted")
		}
	}
	for _, data := range []string{`{}`, `{"formatVersion":2}`, `{"formatVersion":1,"rules":[{}]}`} {
		if _, err := parseRulePreview(data); err == nil {
			t.Fatal("bad import preview accepted")
		}
	}
	doc, err := parseRulePreview(`{"formatVersion":1,"rules":[{"Name":"Test","Dir":"Inbound","Proto":"TCP","LocalPort":"80"}]}`)
	if err != nil || len(doc.Rules) != 1 || doc.Rules[0].port() != "80" {
		t.Fatal("real export contract rejected")
	}
}
func TestReadPlansNeverContainSystemMutations(t *testing.T) {
	for _, p := range pages {
		for _, id := range pageReads(p.ID) {
			op, ok := actionByID(id)
			if !ok || !op.Read {
				t.Fatalf("unsafe page initialization: %s", id)
			}
		}
	}
}
func TestConfirmationClonesCallerArguments(t *testing.T) {
	a := testApplication(t)
	a.page = "connections"
	args := map[string]string{"pid": "1234"}
	a.requestArgs("connections.terminate", args)
	args["pid"] = "9999"
	if a.pending.Args["pid"] != "1234" {
		t.Fatal("confirmation aliases mutable row state")
	}
}

type stubEngine struct {
	respond func(string, map[string]string, bool) (json.RawMessage, error)
}

func (s *stubEngine) call(id string, args map[string]string, confirmed bool, _ string) (json.RawMessage, error) {
	return s.respond(id, args, confirmed)
}
func (s *stubEngine) close() error { return nil }
func pumpUpdate(t *testing.T, updates <-chan func()) {
	t.Helper()
	select {
	case fn := <-updates:
		fn()
	case <-time.After(5 * time.Second):
		t.Fatal("UI update did not arrive")
	}
}
func TestEnteringNetworkLoadsListAndSelectedConfiguration(t *testing.T) {
	a := testApplication(t)
	updates := make(chan func(), 10)
	a.postUpdate = func(fn func()) { updates <- fn }
	calls := []string{}
	a.engine = &stubEngine{respond: func(id string, args map[string]string, confirmed bool) (json.RawMessage, error) {
		calls = append(calls, id)
		if confirmed {
			return nil, fmt.Errorf("page initialization must never confirm changes")
		}
		if id == "network.read" && args["adapterId"] != "11111111-1111-1111-1111-111111111111" {
			return nil, fmt.Errorf("wrong adapter")
		}
		return []byte(featureFixtures[id]), nil
	}}
	a.navigate("network")
	if !a.busy {
		t.Fatal("navigation did not start automatic load")
	}
	pumpUpdate(t, updates)
	pumpUpdate(t, updates)
	if a.busy || strings.Join(calls, ",") != "network.list,network.read" || *a.value("network.apply", "ip") != "192.0.2.20" {
		t.Fatal("automatic network read failed")
	}
	a.setField("network.apply", "dns", "203.0.113.53")
	a.navigate("dashboard")
	pumpUpdate(t, updates)
	a.navigate("network")
	pumpUpdate(t, updates)
	pumpUpdate(t, updates)
	if *a.value("network.apply", "dns") != "203.0.113.53" {
		t.Fatal("navigation refresh destroyed unsubmitted draft")
	}
}
func TestMutationRefreshesRealStatusAndKeepsFailure(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "hyperv"
	updates := make(chan func(), 10)
	a.postUpdate = func(fn func()) { updates <- fn }
	calls := []string{}
	a.engine = &stubEngine{respond: func(id string, _ map[string]string, confirmed bool) (json.RawMessage, error) {
		calls = append(calls, id)
		if id == "hyperv.adapters" {
			if !confirmed {
				return nil, fmt.Errorf("missing confirmation")
			}
			return nil, fmt.Errorf("partial fixture failure")
		}
		return []byte(featureFixtures[id]), nil
	}}
	op, _ := actionByID("hyperv.adapters")
	a.perform(op, map[string]string{"ids": "fixture", "enable": "false"}, false)
	if a.busy || len(calls) > 0 {
		t.Fatal("unconfirmed mutation reached engine")
	}
	a.perform(op, map[string]string{"ids": "fixture", "enable": "false"}, true)
	pumpUpdate(t, updates)
	pumpUpdate(t, updates)
	if a.busy || !a.failed || a.status != "partial fixture failure" || strings.Join(calls, ",") != "hyperv.adapters,hyperv.status" {
		t.Fatal("partial mutation was not refreshed and retained as failed")
	}
}
func TestAutomaticReadFailureHidesPreviousData(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	updates := make(chan func(), 10)
	a.postUpdate = func(fn func()) { updates <- fn }
	a.engine = &stubEngine{respond: func(string, map[string]string, bool) (json.RawMessage, error) {
		return nil, fmt.Errorf("fixture unavailable")
	}}
	a.navigate("hyperv")
	pumpUpdate(t, updates)
	if a.busy || a.readErrors["hyperv.status"] != "fixture unavailable" {
		t.Fatal("failed navigation read not retained")
	}
	a.prefs.Language = "en-US"
	tt := ui.NewTester(a.view, 1400, 1000)
	if tt.HasText("vEthernet fixture") || !tt.HasText("fixture unavailable") {
		t.Fatal("failure looks like a healthy cache")
	}
}

func TestLargeImportConfirmationIsBoundedAndKeepsExactPayload(t *testing.T) {
	a := testApplication(t)
	a.page = "transfer"
	text := `{"formatVersion":1,"rules":[{"Name":"` + strings.Repeat("x", 10000) + `","Dir":"Inbound","Proto":"TCP"}]}`
	a.requestArgs("firewall.import", map[string]string{"json": text})
	if !a.confirming || a.pending.Args["json"] != text || len(a.pending.Description) > 2000 || !strings.Contains(a.pending.Description, "1 rules;") || !strings.Contains(a.pending.Description, "SHA-256") {
		t.Fatal("import confirmation is unbounded or changed payload")
	}
	tt := ui.NewTester(a.view, 480, 480)
	tt.Key(0, ui.KeyEnter)
	if a.confirming || a.busy {
		t.Fatal("large import confirmation lost safe cancellation")
	}
}
func TestFirewallRowEditPopulatesOriginalRule(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "firewall"
	a.prefs.Language = "en-US"
	tt := ui.NewTester(a.view, 1400, 2000)
	if err := tt.Click("Edit rule"); err != nil {
		t.Fatal(err)
	}
	if *a.value("firewall.modify", "oldName") != "SSH fixture" || *a.value("firewall.modify", "port") != "22" || !*a.expanded("rule-editor") {
		t.Fatal("edit form not bound to selected rule")
	}
	if err := tt.Click("Modify rule"); err != nil {
		t.Fatal(err)
	}
	if !a.confirming || a.pending.Args["oldName"] != "SSH fixture" || a.pending.Args["port"] != "22" {
		t.Fatal("edit button did not freeze original rule")
	}
}
func TestWslRowStartsActualDistribution(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "wsl"
	a.prefs.Language = "en-US"
	tt := ui.NewTester(a.view, 1400, 2000)
	if err := tt.Click("Start distribution"); err != nil {
		t.Fatal(err)
	}
	if !a.confirming || a.pending.Action.ID != "wsl.start" || a.pending.Args["distribution"] != "Ubuntu fixture" {
		t.Fatal("WSL row did not bind actual distribution")
	}
}
func TestHttpUsesDedicatedAddressesAndActiveRestartSettings(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "http"
	a.prefs.Language = "en-US"
	tt := ui.NewTester(a.view, 1400, 2000)
	for _, text := range []string{"http://192.0.2.20:8980/", "Copy address", "Open address", "Requests", "7", "Stop / clean firewall rule"} {
		if !tt.HasText(text) {
			t.Fatalf("missing dedicated HTTP control: %s", text)
		}
	}
	if *a.value("http.restart", "path") != "C:/HTTP fixture" || *a.value("http.restart", "port") != "8980" {
		t.Fatal("restart did not use active server settings")
	}
}

func TestDhcpFormDoesNotPretendToApplyManualDns(t *testing.T) {
	a := testApplication(t)
	populateFeatures(t, a)
	a.page = "network"
	a.prefs.Language = "en-US"
	a.setField("network.apply", "mode", "DHCP")
	tt := ui.NewTester(a.view, 1400, 2000)
	if !tt.HasText("DHCP mode also restores automatic DNS") || tt.HasText("Primary DNS") {
		t.Fatal("DHCP UI offers unsupported manual DNS")
	}
	op, _ := actionByID("network.apply")
	a.request(op)
	if !a.confirming || a.pending.Args["dns"] != "" || a.pending.Args["ip"] != "" || a.pending.Args["adapterId"] != a.uiState.networkID {
		t.Fatal("DHCP confirmation contains ignored manual settings or wrong GUID")
	}
}
