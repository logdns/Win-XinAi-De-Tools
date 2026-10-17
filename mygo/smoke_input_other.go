//go:build !windows

package main

import (
	"fmt"

	"github.com/egoist/mygo"
)

func smokeKey(_ *mygo.Window, _ uintptr) error {
	return fmt.Errorf("native smoke input requires Windows")
}
