package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/egoist/mygo"
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
	update := func(fn func()) { done := make(chan struct{}); a.win.Update(func() { fn(); close(done) }); <-done }
	for _, id := range []string{"health", "dashboard", "network.list", "hyperv.status", "wsl.status", "http.discover"} {
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
			a.loadChoices(id, data)
			for pageID, group := range actions {
				for _, op := range group {
					if op.ID == id {
						a.results[pageID] = string(data)
					}
				}
			}
		})
		report = append(report, "PASS read-only engine operation: "+id)
	}
	combinations := 0
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
					})
					time.Sleep(100 * time.Millisecond)
					image, err := a.win.CapturePage()
					if err != nil || len(image) < 1000 {
						fail(fmt.Errorf("native capture %s: %v", page.ID, err))
						return
					}
					if page.ID == "dashboard" || page.ID == "reset" || page.ID == "hyperv" {
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
				}
			}
		}
	}
	report = append(report, fmt.Sprintf("PASS: %d actual MyGo native page/language/theme/size captures; no system mutations", combinations))
	data, _ := json.MarshalIndent(report, "", "  ")
	_ = os.WriteFile(filepath.Join(directory, "verification.json"), data, 0600)
	update(func() { a.busy = false })
	mygo.App.Quit()
}
