package main

import (
	"testing"

	"github.com/egoist/mygo"
	"github.com/egoist/mygo/ui"
)

func TestInitialWindowFitsWorkArea(t *testing.T) {
	for _, work := range []mygo.Rectangle{
		{Width: 1920, Height: 1040}, {Width: 1366, Height: 728},
		{Width: 1024, Height: 728}, {Width: 800, Height: 560},
		{Width: 640, Height: 440}, {Width: 480, Height: 320},
		{X: -1920, Y: 40, Width: 1280, Height: 680},
	} {
		opts := initialWindowOptions(work, true)
		if opts.X < work.X || opts.Y < work.Y || opts.X+opts.Width > work.X+work.Width || opts.Y+opts.Height > work.Y+work.Height {
			t.Fatalf("window exceeds work area: %+v / %+v", opts, work)
		}
		if opts.MinWidth > opts.Width || opts.MinHeight > opts.Height || !opts.Hidden || opts.Frameless || opts.TitleBarStyle != mygo.TitleBarDefault {
			t.Fatalf("invalid initial options: %+v", opts)
		}
		if opts.DisableMinimize || opts.DisableMaximize || opts.DisableClose || opts.DisableResize {
			t.Fatal("native window controls must remain available")
		}
	}
	if opts := initialWindowOptions(mygo.Rectangle{}, false); opts.Width != 1200 || opts.Hidden {
		t.Fatal("invalid display fallback")
	}
}

func TestNativeCloseChoicesAndGuards(t *testing.T) {
	for _, tray := range []bool{false, true} {
		for _, language := range []string{"zh-CN", "en-US"} {
			a := testApplication(t)
			a.prefs.Language, a.trayAvailable = language, tray
			if a.closePrompt || a.requestClose() || !a.closePrompt {
				t.Fatal("native close must open choice, not exit")
			}
			a.requestClose()
			if a.exitApproved {
				t.Fatal("repeated X approved exit")
			}
			a.navigate("network")
			a.requestArgs("network.reset", nil)
			if a.page != "dashboard" || a.confirming || a.busy {
				t.Fatal("close modal allowed background actions")
			}
			for _, key := range []ui.Key{ui.KeyEnter, ui.KeyEscape} {
				a.requestClose()
				tt := ui.NewTester(a.view, 416, 360)
				if tt.HasText(a.tr("最小化到托盘", "Hide to tray")) != tray {
					t.Fatal("hide must depend on actual tray availability")
				}
				for _, label := range []string{a.tr("退出程序", "Exit application"), a.tr("取消", "Cancel")} {
					r, ok := tt.Find(label)
					if !ok || r.X < 0 || r.Y < 0 || r.X+r.W > 416 || r.Y+r.H > 360 {
						t.Fatalf("dialog control outside compact window: %s %+v", label, r)
					}
				}
				tt.Key(0, key)
				if a.closePrompt || a.exitApproved {
					t.Fatal("Enter/Escape did not cancel safely")
				}
			}
			a.requestClose()
			wrap := ui.NewTester(a.view, 640, 480)
			wrap.Key(0, ui.KeyTab)
			first := a.tr("退出程序", "Exit application")
			if tray {
				first = a.tr("最小化到托盘", "Hide to tray")
			}
			if !wrap.Focused(first) {
				t.Fatal("native smoke Tab wrap must select first close action")
			}
			wrap.Key(0, ui.KeyEscape)
			a.requestClose()
			tt := ui.NewTester(a.view, 640, 480)
			// Installer automation uses the same native focus order: Cancel -> Exit.
			tt.Key(ui.Shift, ui.KeyTab)
			if !tt.Focused(a.tr("退出程序", "Exit application")) {
				t.Fatal("Shift+Tab must focus Exit from default Cancel")
			}
			tt.Key(0, ui.KeyEnter)
			if !a.exitApproved || a.closePrompt || !a.requestClose() {
				t.Fatal("explicit exit would prompt recursively")
			}
		}
	}
	for _, confirming := range []bool{false, true} {
		a := testApplication(t)
		a.confirming, a.busy = confirming, !confirming
		if a.requestClose() || a.closePrompt || a.approveExit() || a.exitApproved || a.status == "" {
			t.Fatal("active operation/confirmation close guard failed")
		}
	}
}

func TestRedundantChromeRemovedAtAllSizes(t *testing.T) {
	a := testApplication(t)
	for _, language := range []string{"zh-CN", "en-US"} {
		a.prefs.Language = language
		for _, width := range []int{416, 464, 640, 1024} {
			tt := ui.NewTester(a.view, width, 360)
			for _, label := range []string{"XINAI / MYGO", "XINAI / NETWORK", "Win-XinAi-De-Tools-MyGo", "Win-XinAi / MyGo", a.tr("返回", "Back"), a.tr("最小化", "Minimize"), a.tr("最大化/还原", "Maximize/restore"), a.tr("关闭/退出", "Close/exit"), a.tr("关闭窗口", "Close window")} {
				if tt.HasText(label) {
					t.Fatalf("redundant or premature chrome %s at %d", label, width)
				}
			}
			if width < 760 && !tt.HasText(a.tr("页面", "Page")) {
				t.Fatal("compact navigation removed")
			}
		}
	}
}

func TestBackShortcutRetainsHistoryWithoutHeader(t *testing.T) {
	a := testApplication(t)
	a.navigate("hyperv")
	a.navigate("reset")
	tt := ui.NewTester(a.view, 640, 480)
	tt.Key(ui.Alt, ui.KeyLeft)
	if a.page != "hyperv" {
		t.Fatal("Alt+Left lost visited page")
	}
	a.requestClose()
	tt.Frame()
	tt.Key(ui.Alt, ui.KeyLeft)
	if a.page != "hyperv" {
		t.Fatal("Back escaped close modal")
	}
}
