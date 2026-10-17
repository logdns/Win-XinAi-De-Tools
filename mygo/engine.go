package main

import (
	"bufio"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"sync"
	"time"
)

type engineClient struct {
	mu     sync.Mutex
	cmd    *exec.Cmd
	in     io.WriteCloser
	out    *bufio.Scanner
	pipe   io.ReadCloser
	id     int64
	failed error
}

func startEngine() (*engineClient, error) {
	executable, err := os.Executable()
	if err != nil {
		return nil, err
	}
	path := filepath.Join(filepath.Dir(executable), "engine", "Win-XinAi-De-Tools.Engine.exe")
	cmd := exec.Command(path)
	hideProcess(cmd)
	cmd.Dir = filepath.Dir(path)
	cmd.Stderr = os.Stderr
	in, err := cmd.StdinPipe()
	if err != nil {
		return nil, err
	}
	out, err := cmd.StdoutPipe()
	if err != nil {
		_ = in.Close()
		return nil, err
	}
	if err := cmd.Start(); err != nil {
		_ = in.Close()
		return nil, fmt.Errorf("start shared engine: %w", err)
	}
	scanner := bufio.NewScanner(out)
	scanner.Buffer(make([]byte, 64*1024), 32*1024*1024)
	return &engineClient{cmd: cmd, in: in, out: scanner, pipe: out}, nil
}

func (e *engineClient) call(op string, args map[string]string, confirmed bool, language string) (json.RawMessage, error) {
	e.mu.Lock()
	defer e.mu.Unlock()
	if e.failed != nil {
		return nil, e.failed
	}
	timeout := 5 * time.Minute
	if len(op) >= 4 && op[:4] == "wsl." {
		timeout = time.Hour // Installation and distribution exports can be large.
	}
	if op == "shutdown" || op == "health" {
		timeout = 15 * time.Second
	}
	type outcome struct {
		data json.RawMessage
		err  error
	}
	done := make(chan outcome, 1)
	go func() {
		data, err := e.exchange(op, args, confirmed, language)
		done <- outcome{data, err}
	}()
	timer := time.NewTimer(timeout)
	defer timer.Stop()
	select {
	case result := <-done:
		return result.data, result.err
	case <-timer.C:
		_ = e.cmd.Process.Kill()
		_ = e.in.Close()
		_ = e.pipe.Close()
		<-done
		e.failed = errors.New("shared engine timed out; changes may be partial. Inspect Windows state and firewall rules locally before retrying")
		return nil, e.failed
	}
}

// The caller owns mu until the whole exchange (including timeout cleanup) ends.
func (e *engineClient) exchange(op string, args map[string]string, confirmed bool, language string) (json.RawMessage, error) {
	e.id++
	request := struct {
		ID        int64             `json:"id"`
		Op        string            `json:"op"`
		Args      map[string]string `json:"args"`
		Confirmed bool              `json:"confirmed"`
		Language  string            `json:"language"`
	}{e.id, op, args, confirmed, language}
	data, err := json.Marshal(request)
	if err != nil {
		return nil, err
	}
	if len(data) > 1_048_576 {
		return nil, errors.New("request exceeds 1 MiB")
	}
	if _, err = e.in.Write(append(data, '\n')); err != nil {
		e.failed = err
		return nil, err
	}
	if !e.out.Scan() {
		e.failed = fmt.Errorf("shared engine disconnected: %v", e.out.Err())
		return nil, e.failed
	}
	var response struct {
		ID     int64           `json:"id"`
		OK     bool            `json:"ok"`
		Result json.RawMessage `json:"result"`
		Error  string          `json:"error"`
	}
	if err := json.Unmarshal(e.out.Bytes(), &response); err != nil {
		e.failed = err
		return nil, err
	}
	if response.ID != e.id {
		e.failed = errors.New("engine response ID mismatch")
		return nil, e.failed
	}
	if !response.OK {
		return nil, errors.New(response.Error)
	}
	return response.Result, nil
}

func (e *engineClient) close() error {
	_, err := e.call("shutdown", nil, false, "en-US")
	_ = e.in.Close()
	wait := make(chan error, 1)
	go func() { wait <- e.cmd.Wait() }()
	select {
	case result := <-wait:
		if err == nil {
			err = result
		}
	case <-time.After(5 * time.Second):
		_ = e.cmd.Process.Kill()
		_ = e.pipe.Close()
		err = errors.New("shared engine shutdown timed out; inspect temporary HTTP firewall rules locally")
	}
	return err
}
