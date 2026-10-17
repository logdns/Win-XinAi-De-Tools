package main

import (
	"log"

	"github.com/egoist/mygo"
	"github.com/egoist/mygo/ui"
)

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

func (a *application) canExit() bool {
	if a.busy || a.confirming {
		a.status = a.tr("请等待当前操作完成后退出。", "Wait for the active operation before exiting.")
		return false
	}
	return true
}

// Only an explicit Exit choice may bypass the next native close notification.
func (a *application) requestClose() bool {
	if !a.canExit() {
		return false
	}
	if a.exitApproved {
		return true
	}
	a.closePrompt = true
	log.Printf("MyGo close choice opened; tray=%t", a.trayAvailable)
	return false
}

func (a *application) approveExit() bool {
	if !a.canExit() {
		return false
	}
	a.closePrompt = false
	a.exitApproved = true
	log.Print("MyGo exit approved")
	return true
}

func (a *application) closeDialog(c *ui.Context) {
	buttons := []string{}
	if a.trayAvailable {
		buttons = append(buttons, a.tr("最小化到托盘", "Hide to tray"))
	}
	buttons = append(buttons, a.tr("退出程序", "Exit application"), a.tr("取消", "Cancel"))
	chosen := -1
	width, height := c.Size()
	title := a.tr("关闭窗口", "Close window")
	message := a.tr("请选择关闭方式。退出将停止临时 HTTP 服务。", "Choose what to do. Exiting stops the temporary HTTP service.")
	// The SDK alert has a fixed 300-DIP panel; localized three-button rows
	// overflow it. Use native dialog primitives with bounded, wrapping content.
	dialog := ui.DialogBase(c.Key("close-choice"), &a.closePrompt, func(back, panel ui.Element) {
		back.Background(ui.RGBA(0, 0, 0, 0.4))
		panel.Column().Width(420).MaxWidth(width-32).MaxHeight(height-32).Padding(20).Gap(16).
			Radius(12).Background(c.Theme().Background).Border(1, c.Theme().Border).
			Shadow(0, 10, 30, 0, ui.RGBA(0, 0, 0, 0.3)).Role(ui.RoleAlertDialog).Label(title).Description(message)
		ui.Text(c, title).Bold().FontSize(18)
		ui.Text(c, message).TextColor(c.Theme().TextMuted)
		ui.Row(c).FillWidth().Gap(8).Wrap().Justify(ui.End).Children(func() {
			for i, label := range buttons {
				button := ui.Element{}
				if i == len(buttons)-1 {
					button = ui.PrimaryButton(c, label).AutoFocus()
				} else {
					button = ui.Button(c, label)
				}
				if button.Clicked() {
					chosen = i
					a.closePrompt = false
				}
			}
		})
	})
	if dialog.Dismissed() {
		chosen = ui.AlertDismissed
	}
	if chosen == ui.AlertDismissed || chosen == len(buttons)-1 {
		log.Print("MyGo close choice canceled")
	}
	if chosen < 0 || chosen == len(buttons)-1 {
		return
	}
	if a.trayAvailable && chosen == 0 {
		if a.canExit() && a.win != nil {
			a.win.Hide()
			log.Print("MyGo hidden to tray")
		}
	} else if a.approveExit() && a.win != nil {
		// Quit is deferred until this UI frame completes and uses normal cleanup.
		mygo.App.Quit()
	}
}
