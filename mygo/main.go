package main

import (
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"runtime"
	"strings"

	"github.com/egoist/mygo"
	"github.com/egoist/mygo/ui"
)

func main() {
	if runtime.GOOS != "windows" {
		fmt.Fprintln(os.Stderr, "The shipped MyGo edition manages Windows only.")
		return
	}
	mygo.App.SetName("Win-XinAi-De-Tools-MyGo")
	if !mygo.App.RequestSingleInstanceLock() {
		return
	}
	a := newApplication()
	logRoot := filepath.Dir(preferencePath())
	_ = os.MkdirAll(logRoot, 0700)
	if file, err := os.OpenFile(filepath.Join(logRoot, "startup.log"), os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0600); err == nil {
		defer file.Close()
		log.SetOutput(file)
	}
	var err error
	client, err := startEngine()
	if err == nil {
		a.engine = client
		var health struct{ Version string }
		var data json.RawMessage
		data, err = a.engine.call("health", nil, false, "en-US")
		if err == nil {
			err = json.Unmarshal(data, &health)
			if err == nil && health.Version != version {
				err = fmt.Errorf("shared engine version mismatch: expected %s, received %s", version, health.Version)
			}
		}
		if err != nil {
			_ = a.engine.close()
			a.engine = nil
		}
	}
	if err != nil {
		a.status = err.Error()
		a.failed = true
		log.Print(err)
	}
	smoke := false
	silent := false
	for _, arg := range os.Args[1:] {
		if arg == "--ui-smoke" && os.Getenv("CI") == "true" {
			smoke = true
		}
		if strings.EqualFold(arg, "/silent") {
			silent = true
		}
	}
	mygo.App.OnSecondInstance(func(_ []string, _ string) {
		if a.win != nil {
			a.win.Show()
			a.win.Restore()
			a.win.Focus()
		}
	})
	mygo.App.WhenReady(func() {
		a.win = mygo.NewWindow(mygo.WindowOptions{Title: "Win-XinAi-De-Tools-MyGo", Width: 1200, Height: 850, MinWidth: 480, MinHeight: 480, Hidden: silent, Page: mygo.PageOptions{DevTools: mygo.DevToolsDisabled}, Content: ui.View(a.view)})
		if executable, err := os.Executable(); err == nil {
			if icon, err := os.ReadFile(filepath.Join(filepath.Dir(executable), "Assets", "Square150x150Logo.png")); err == nil {
				_ = a.win.SetIcon(icon)
				if _, err := mygo.NewTray(mygo.TrayOptions{Icon: icon, ToolTip: "Win-XinAi-De-Tools-MyGo", Menu: mygo.NewMenu([]*mygo.MenuItem{
					{Label: a.tr("显示窗口", "Show window"), Click: func(*mygo.MenuItem, *mygo.Window) { a.win.Show(); a.win.Restore(); a.win.Focus() }},
					{Label: a.tr("隐藏到托盘", "Hide to tray"), Click: func(*mygo.MenuItem, *mygo.Window) { a.win.Hide() }},
					{Role: mygo.RoleQuit},
				})}); err != nil {
					log.Print(err)
					a.win.Show()
				}
			} else {
				a.win.Show()
			}
		}
		a.win.OnClose(func(e *mygo.CloseEvent) {
			if a.busy {
				e.PreventDefault()
				a.status = a.tr("请等待当前操作完成后退出。", "Wait for the active operation before exiting.")
				a.win.Invalidate()
			}
		})
		log.Printf("MyGo native window initialized; version=%s; SDK=%s; production=%t; no WebView", version, mygo.App.Version(), !mygo.IsDev())
		if smoke {
			go runSmoke(a)
		} else if a.engine != nil {
			op, _ := actionByID("dashboard")
			a.perform(op, nil, false)
		}
	})
	if err := mygo.App.Run(); err != nil {
		log.Print(err)
	}
	if a.engine != nil {
		if err := a.engine.close(); err != nil {
			log.Print("Engine shutdown: ", err)
		}
	}
	log.Print("MyGo shutdown completed")
}
