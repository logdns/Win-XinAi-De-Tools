package main

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

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

func TestEveryOperationFormBuilds(t *testing.T) {
	a := testApplication(t)
	a.prefs.Language = "en-US"
	for page, group := range actions {
		a.page = page
		for _, op := range group {
			id := op.ID
			a.selected[page] = &id
			tester := ui.NewTester(a.view, 1400, 1000)
			if !tester.HasText(op.EN) {
				t.Fatalf("missing operation: %s", op.ID)
			}
			for _, key := range actionFields(op) {
				if _, exists := fieldNames[key]; !exists {
					t.Fatalf("missing bilingual field: %s", key)
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
	if err := tt.Click("Run selected operation"); err != nil {
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
