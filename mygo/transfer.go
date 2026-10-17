package main

import (
	"fmt"
	"io"
	"os"

	"github.com/egoist/mygo"
)

func (a *application) pickRuleFile() {
	if a.win == nil || a.busy {
		return
	}
	a.busy = true
	go func() {
		paths, err := mygo.Dialog.Open(mygo.OpenDialogOptions{Parent: a.win, Filters: []mygo.FileFilter{{Name: "JSON", Extensions: []string{"json"}}}})
		var data []byte
		if err == nil && len(paths) > 0 {
			var file *os.File
			file, err = os.Open(paths[0])
			if err == nil {
				info, statErr := file.Stat()
				if statErr != nil {
					err = statErr
				} else if !info.Mode().IsRegular() {
					err = fmt.Errorf("Select a regular JSON file")
				} else if info.Size() > 900_000 {
					err = fmt.Errorf("Rule file is too large (maximum 900 KB)")
				}
				if err == nil {
					data, err = io.ReadAll(io.LimitReader(file, 900_001))
				}
				_ = file.Close()
				if len(data) > 900_000 {
					err = fmt.Errorf("Rule file is too large (maximum 900 KB)")
				}
			}
			if err == nil {
				_, err = parseRulePreview(string(data))
			}
		}
		a.win.Update(func() {
			a.busy = false
			if err != nil {
				a.failed = true
				a.status = err.Error()
			} else if len(paths) > 0 {
				a.setField("firewall.import", "json", string(data))
			}
		})
	}()
}
