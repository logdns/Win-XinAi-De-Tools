package main

import "github.com/egoist/mygo"

// WorkArea and WindowOptions both use DIPs; do not multiply by ScaleFactor.
func initialWindowOptions(work mygo.Rectangle, hidden bool) mygo.WindowOptions {
	opts := mygo.WindowOptions{
		Title: "Win-XinAi-De-Tools-MyGo", Width: 1200, Height: 850,
		MinWidth: 480, MinHeight: 360, Hidden: hidden,
		TitleBarStyle: mygo.TitleBarDefault,
		Page:          mygo.PageOptions{DevTools: mygo.DevToolsDisabled},
	}
	if work.Width > 0 && work.Height > 0 {
		margin := min(24, work.Width/20, work.Height/20)
		opts.Width = min(opts.Width, work.Width-2*margin)
		opts.Height = min(opts.Height, work.Height-2*margin)
		opts.MinWidth = min(opts.MinWidth, opts.Width)
		opts.MinHeight = min(opts.MinHeight, opts.Height)
		opts.X = work.X + (work.Width-opts.Width)/2
		opts.Y = work.Y + (work.Height-opts.Height)/2
	}
	return opts
}

type windowControls interface {
	Minimize()
	Maximize()
	Unmaximize()
	IsMaximized() bool
	Close()
}

func (a *application) controlWindow(w windowControls, command string) {
	if command == "close" && a.busy {
		a.status = a.tr("请等待当前操作完成后退出。", "Wait for the active operation before exiting.")
		return
	}
	if w == nil {
		return
	}
	switch command {
	case "minimize":
		w.Minimize()
	case "maximize":
		if w.IsMaximized() {
			w.Unmaximize()
		} else {
			w.Maximize()
		}
	case "close":
		w.Close()
	}
}
