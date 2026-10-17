//go:build windows

package main

import (
	"fmt"
	"syscall"
	"time"

	"github.com/egoist/mygo"
)

// Drive the real HWND input path, not the fixture UI tester.
func smokeKey(w *mygo.Window, key uintptr) error {
	post := syscall.NewLazyDLL("user32.dll").NewProc("PostMessageW")
	for _, message := range []uintptr{0x0100, 0x0101} {
		ok, _, err := post.Call(w.NativeHandle(), message, key, 0)
		if ok == 0 {
			return fmt.Errorf("native key input: %w", err)
		}
		time.Sleep(100 * time.Millisecond)
	}
	return nil
}
