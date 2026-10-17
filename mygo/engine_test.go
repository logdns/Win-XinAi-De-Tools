package main

import (
	"bufio"
	"bytes"
	"encoding/json"
	"strings"
	"testing"
)

type testInput struct{ bytes.Buffer }

func (*testInput) Close() error { return nil }

func fixtureEngine(response string) (*engineClient, *testInput) {
	in := &testInput{}
	return &engineClient{in: in, out: bufio.NewScanner(strings.NewReader(response))}, in
}

func TestEngineCorrelatesResponseAndWritesConfirmation(t *testing.T) {
	e, in := fixtureEngine("{\"id\":1,\"ok\":true,\"result\":{\"completed\":true}}\n")
	data, err := e.call("network.reset", map[string]string{"mode": "FlushDns"}, true, "zh-CN")
	if err != nil || !strings.Contains(string(data), "completed") {
		t.Fatalf("response: %s %v", data, err)
	}
	var request struct {
		ID        int
		Confirmed bool
		Op        string
	}
	if err := json.Unmarshal(in.Bytes(), &request); err != nil {
		t.Fatal(err)
	}
	if request.ID != 1 || !request.Confirmed || request.Op != "network.reset" {
		t.Fatal("request contract lost")
	}
}

func TestDisconnectedOrInvalidEngineCannotBeReused(t *testing.T) {
	for _, response := range []string{"", "not-json\n", "{\"id\":2,\"ok\":true}\n"} {
		e, in := fixtureEngine(response)
		if _, err := e.call("health", nil, false, "en-US"); err == nil {
			t.Fatal("invalid response accepted")
		}
		before := in.Len()
		if _, err := e.call("health", nil, false, "en-US"); err == nil || before != in.Len() {
			t.Fatal("failed connection reused")
		}
	}
}

func TestEngineErrorsAndOversizedRequestsAreNotReportedAsSuccess(t *testing.T) {
	e, in := fixtureEngine("{\"id\":1,\"ok\":false,\"error\":\"fixture denied\"}\n")
	if _, err := e.call("health", nil, false, "en-US"); err == nil || err.Error() != "fixture denied" {
		t.Fatal("engine error hidden")
	}
	before := in.Len()
	if _, err := e.call("health", map[string]string{"large": strings.Repeat("a", 1_048_576)}, false, "en-US"); err == nil || in.Len() != before {
		t.Fatal("oversized request sent")
	}
}
