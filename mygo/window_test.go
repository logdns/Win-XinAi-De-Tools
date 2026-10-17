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

type testWindow struct {
	minimized, maximized, closed bool
}

func (w *testWindow) Minimize()         { w.minimized = true }
func (w *testWindow) Maximize()         { w.maximized = true }
func (w *testWindow) Unmaximize()       { w.maximized = false }
func (w *testWindow) IsMaximized() bool { return w.maximized }
func (w *testWindow) Close()            { w.closed = true }

func TestWindowControlsAndBusyCloseGuard(t *testing.T) {
	a := testApplication(t)
	w := &testWindow{}
	a.controlWindow(w, "minimize")
	a.controlWindow(w, "maximize")
	if !w.minimized || !w.maximized {
		t.Fatal("minimize/maximize not forwarded")
	}
	a.controlWindow(w, "maximize")
	if w.maximized {
		t.Fatal("maximize did not toggle restore")
	}
	a.busy = true
	a.controlWindow(w, "close")
	if w.closed || a.status == "" {
		t.Fatal("busy operation can be interrupted")
	}
	a.busy = false
	a.controlWindow(w, "close")
	if !w.closed {
		t.Fatal("close did not use normal guarded window lifecycle")
	}
}

func TestWindowControlsVisibleAtCompactSizes(t *testing.T) {
	a := testApplication(t)
	for _, language := range []string{"zh-CN", "en-US"} {
		a.prefs.Language = language
		for _, width := range []int{416, 464, 640, 1024} {
			tester := ui.NewTester(a.view, width, 360)
			for _, label := range []string{a.tr("最小化", "Minimize"), a.tr("最大化/还原", "Maximize/restore"), a.tr("关闭/退出", "Close/exit")} {
				r, found := tester.Find(label)
				if !found || r.X < 0 || r.Y < 0 || r.X+r.W > float32(width) || r.Y+r.H > 360 {
					t.Fatalf("missing visible window control %s at %d", label, width)
				}
			}
		}
	}
}
