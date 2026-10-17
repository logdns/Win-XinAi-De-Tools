//go:build windows

package main

import (
	"fmt"
	"syscall"
	"time"
	"unsafe"

	"github.com/egoist/mygo"
)

// Drive the real HWND input path, not the fixture UI tester.
func smokeKey(w *mygo.Window, key uintptr) error {
	user := syscall.NewLazyDLL("user32.dll")
	class, _ := syscall.UTF16PtrFromString("MyGoSurface")
	// MyGo routes keyboard events to its native child surface, not the frame.
	surface, _, err := user.NewProc("FindWindowExW").Call(w.NativeHandle(), 0, uintptr(unsafe.Pointer(class)), 0)
	if surface == 0 {
		return fmt.Errorf("native MyGo input surface not found: %w", err)
	}
	post := user.NewProc("PostMessageW")
	for _, message := range []uintptr{0x0100, 0x0101} {
		ok, _, err := post.Call(surface, message, key, 0)
		if ok == 0 {
			return fmt.Errorf("native key input: %w", err)
		}
		time.Sleep(100 * time.Millisecond)
	}
	return nil
}
