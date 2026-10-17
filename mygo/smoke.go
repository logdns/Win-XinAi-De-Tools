package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"image/png"
	"os"
	"path/filepath"
	"time"

	"github.com/egoist/mygo"
	"github.com/egoist/mygo/ui"
)

// This hook visits the actual native Windows window, never a WebView or mock backend.
func runSmoke(a *application) {
	directory := os.Getenv("WINXINAI_MYGO_SMOKE_DIR")
	if directory == "" {
		directory = "artifacts/mygo-ui"
	}
	_ = os.MkdirAll(directory, 0700)
	report := []string{}
	fail := func(err error) {
		_ = os.WriteFile(filepath.Join(directory, "FAILED.txt"), []byte(err.Error()), 0600)
		mygo.App.Exit(1)
	}
	if a.engine == nil {
		fail(fmt.Errorf("shared engine not running"))
		return
	}
	if mygo.IsDev() || !mygo.App.IsPackaged() || !matchesReleaseVersion(mygo.App.Version()) {
		fail(fmt.Errorf("MyGo production/package version mismatch: dev=%t packaged=%t SDK=%s expected=%s", mygo.IsDev(), mygo.App.IsPackaged(), mygo.App.Version(), version))
		return
	}
	report = append(report, "PASS production build: SDK version matches; native inspector excluded; per-window DevTools disabled")
	update := func(fn func()) { done := make(chan struct{}); a.win.Update(func() { fn(); close(done) }); <-done }
	var windowError error
	update(func() {
		work := mygo.Screen.PrimaryDisplay().WorkArea
		bounds := a.win.Bounds()
		if bounds.X < work.X || bounds.Y < work.Y || bounds.X+bounds.Width > work.X+work.Width || bounds.Y+bounds.Height > work.Y+work.Height {
			windowError = fmt.Errorf("initial window exceeds work area: window=%+v work=%+v", bounds, work)
		}
		report = append(report, fmt.Sprintf("Initial window=%+v; work area=%+v (DIPs)", bounds, work))
	})
	if windowError != nil {
		fail(windowError)
		return
	}
	a.win.Minimize()
	time.Sleep(150 * time.Millisecond)
	if !a.win.IsMinimized() {
		fail(fmt.Errorf("window minimize failed"))
		return
	}
	a.win.Restore()
	a.win.Maximize()
	time.Sleep(150 * time.Millisecond)
	if !a.win.IsMaximized() {
		fail(fmt.Errorf("window maximize failed"))
		return
	}
	a.win.Unmaximize()
	a.win.Restore()
	a.win.Focus()
	update(func() { a.busy = true })
	a.win.Close()
	if a.win.IsDestroyed() {
		fail(fmt.Errorf("busy close guard failed"))
		return
	}
	update(func() { a.busy = false; a.status = "" })
	report = append(report, "PASS initial work-area fit; actual minimize/maximize/restore; busy close veto")
	key := func(code uintptr) {
		if err := smokeKey(a.win, code); err != nil {
			fail(err)
		}
	}
	a.win.Close()
	time.Sleep(200 * time.Millisecond)
	update(func() {
		if !a.closePrompt || a.exitApproved {
			windowError = fmt.Errorf("native X did not open close choice")
		}
	})
	if windowError != nil {
		fail(windowError)
		return
	}
	key(0x0D) // Enter must activate the default Cancel.
	update(func() {
		if a.closePrompt || a.exitApproved {
			windowError = fmt.Errorf("native Enter did not cancel closing")
		}
	})
	if windowError != nil {
		fail(windowError)
		return
	}
	if a.trayAvailable {
		a.win.Close()
		time.Sleep(200 * time.Millisecond)
		key(0x09) // Cancel -> first button (Hide to tray).
		key(0x0D)
		if a.win.IsVisible() || a.win.IsDestroyed() {
			fail(fmt.Errorf("native hide-to-tray choice failed"))
			return
		}
		a.win.Show()
		a.win.Restore()
		a.win.Focus()
		report = append(report, "PASS native HWND keyboard choices: X -> Cancel (default); X -> Hide to tray; show/restore")
	}
	for _, id := range []string{"health", "dashboard", "firewall.list", "network.list", "hyperv.status", "wsl.status", "smb.status", "smb.addresses", "http.status", "connections.list", "audit.list", "http.discover"} {
		data, err := a.engine.call(id, nil, false, "en-US")
		if err != nil {
			fail(fmt.Errorf("%s: %w", id, err))
			return
		}
		var value any
		if err := json.Unmarshal(data, &value); err != nil {
			fail(err)
			return
		}
		if id == "health" {
			var health struct{ Version string }
			_ = json.Unmarshal(data, &health)
			if health.Version != version {
				fail(fmt.Errorf("engine/UI version mismatch"))
				return
			}
		}
		update(func() {
			if err := a.acceptData(id, data, nil); err != nil {
				fail(err)
				return
			}
			for pageID, group := range actions {
				for _, op := range group {
					if op.ID == id {
						a.results[pageID] = formatResult(data)
					}
				}
			}
		})
		report = append(report, "PASS read-only engine operation: "+id)
	}
	// Read the actual selected adapter too, without changing host configuration.
	if a.uiState.networkID != "" {
		args := map[string]string{"adapterId": a.uiState.networkID}
		data, err := a.engine.call("network.read", args, false, "en-US")
		if err != nil {
			fail(err)
			return
		}
		update(func() {
			if err := a.acceptData("network.read", data, args); err != nil {
				fail(err)
			}
		})
		report = append(report, "PASS selected-adapter IPv4/DNS/route read and editable form population")
	}
	// Verify structured content exists in native controls, not only page titles or raw JSON.
	update(func() {
		for page, needle := range map[string]string{
			"firewall":    a.tr("搜索规则、端口或协议", "Search rules, ports or protocol"),
			"hyperv":      a.tr("平台与启动状态", "Platform and boot status"),
			"network":     a.tr("IP 与 DNS 设置", "IP and DNS settings"),
			"smb":         a.tr("当前 SMB 配置", "Current SMB configuration"),
			"wsl":         a.tr("已安装 WSL", "WSL installed"),
			"http":        a.tr("文件服务状态", "File server status"),
			"connections": a.tr("搜索进程、PID、地址或端口", "Search process, PID, address or port"),
		} {
			a.page = page
			tester := ui.NewTester(a.view, 1400, 900)
			if !tester.HasText(needle) || tester.HasText(a.tr("执行所选操作", "Run selected operation")) {
				fail(fmt.Errorf("%s has no dedicated feature controls", page))
				return
			}
		}
	})
	report = append(report, "PASS dedicated native feature controls with real Windows read-only engine data")
	combinations := 0
	details := 0
	for _, language := range []string{"zh-CN", "en-US"} {
		for _, theme := range []string{"Light", "Dark"} {
			for _, size := range [][2]int{{480, 640}, {800, 600}, {1400, 900}} {
				a.win.SetSize(size[0], size[1])
				for _, page := range pages {
					update(func() {
						a.page = page.ID
						a.prefs.Language = language
						a.prefs.Theme = theme
						a.status = ""
						a.busy = false
						a.pageScroll(page.ID).Y = 0
					})
					time.Sleep(100 * time.Millisecond)
					image, err := a.win.CapturePage()
					if err != nil || len(image) < 1000 {
						fail(fmt.Errorf("native capture %s: %v", page.ID, err))
						return
					}
					if page.ID == "dashboard" {
						config, err := png.DecodeConfig(bytes.NewReader(image))
						if err != nil {
							fail(err)
							return
						}
						report = append(report, fmt.Sprintf("CAPTURE %s/%s requested=%dx%d actual=%dx%d", language, theme, size[0], size[1], config.Width, config.Height))
					}
					{
						name := fmt.Sprintf("%s-%s-%d.png", page.ID, theme, size[0])
						if language == "zh-CN" {
							name = "zh-CN-" + name
						}
						if err := os.WriteFile(filepath.Join(directory, name), image, 0600); err != nil {
							fail(err)
							return
						}
					}
					combinations++
					if size[0] == 1400 && (page.ID == "network" || page.ID == "hyperv" || page.ID == "reset" || page.ID == "http" || page.ID == "firewall") {
						mode := *a.value("network.apply", "mode")
						update(func() {
							if page.ID == "network" {
								a.setField("network.apply", "mode", "Static")
							}
							if page.ID == "hyperv" {
								*a.expanded("hyperv.switch.create") = true
							}
							a.pageScroll(page.ID).Y = 450
						})
						time.Sleep(100 * time.Millisecond)
						capture, err := a.win.CapturePage()
						if err != nil || len(capture) < 1000 {
							fail(fmt.Errorf("detail capture %s: %v", page.ID, err))
							return
						}
						name := fmt.Sprintf("%s-details-%s-%s.png", page.ID, language, theme)
						if err := os.WriteFile(filepath.Join(directory, name), capture, 0600); err != nil {
							fail(err)
							return
						}
						details++
						update(func() {
							a.setField("network.apply", "mode", mode)
							*a.expanded("hyperv.switch.create") = false
							a.pageScroll(page.ID).Y = 0
						})
					}
				}
			}
		}
	}
	report = append(report, fmt.Sprintf("PASS: %d actual MyGo native page/language/theme/size captures; no system mutations", combinations))
	report = append(report, fmt.Sprintf("PASS: %d additional scrolled feature/settings captures; static network form is an unsubmitted draft, no system changes", details))
	data, _ := json.MarshalIndent(report, "", "  ")
	_ = os.WriteFile(filepath.Join(directory, "verification.json"), data, 0600)
	a.win.SetSize(800, 600)
	for _, language := range []string{"en-US", "zh-CN"} {
		update(func() {
			a.busy = false
			a.page = "dashboard"
			a.pageScroll("dashboard").Y = 0
			a.prefs.Language = language
			a.prefs.Theme = "Light"
		})
		a.win.Close()
		time.Sleep(200 * time.Millisecond)
		image, err := a.win.CapturePage()
		if err != nil {
			fail(err)
			return
		}
		if err := os.WriteFile(filepath.Join(directory, "close-choice-"+language+".png"), image, 0600); err != nil {
			fail(err)
			return
		}
		key(0x1B) // Escape is also a safe cancellation on the actual HWND.
		update(func() {
			if a.closePrompt || a.exitApproved {
				windowError = fmt.Errorf("native Escape did not cancel closing")
			}
		})
		if windowError != nil {
			fail(windowError)
			return
		}
	}
	a.win.Close()
	time.Sleep(200 * time.Millisecond)
	// From default Cancel, wrap to Hide (if present), then Exit.
	key(0x09)
	if a.trayAvailable {
		key(0x09)
	}
	key(0x0D)
}

// Windows VERSIONINFO uses four numeric fields even for a three-part release.
func matchesReleaseVersion(value string) bool { return value == version || value == version+".0" }
