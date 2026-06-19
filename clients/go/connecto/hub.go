package connecto

import (
	"encoding/json"
	"fmt"
	"net/url"
	"strings"
	"sync"

	"github.com/gorilla/websocket"
	"github.com/google/uuid"
)

const recordSeparator = '\x1e'

type invocationState struct {
	result chan json.RawMessage
	err    chan error
}

type HubConnection struct {
	conn        *websocket.Conn
	mu          sync.Mutex
	invocations map[string]*invocationState
	broadcasts  map[string]chan json.RawMessage
	done        chan struct{}
}

func NewHubConnection(baseURL, path, token string) (*HubConnection, error) {
	wsURL := strings.ReplaceAll(baseURL, "http://", "ws://")
	wsURL = strings.ReplaceAll(wsURL, "https://", "wss://")

	u, err := url.Parse(wsURL + path + "?access_token=" + token)
	if err != nil {
		return nil, fmt.Errorf("parse url: %w", err)
	}

	conn, _, err := websocket.DefaultDialer.Dial(u.String(), nil)
	if err != nil {
		return nil, fmt.Errorf("dial: %w", err)
	}

	// Send handshake
	handshake, _ := json.Marshal(handshakeRequest{Protocol: "json", Version: 1})
	err = conn.WriteMessage(websocket.TextMessage, append(handshake, byte(recordSeparator)))
	if err != nil {
		return nil, fmt.Errorf("handshake write: %w", err)
	}

	hc := &HubConnection{
		conn:        conn,
		invocations: make(map[string]*invocationState),
		broadcasts:  make(map[string]chan json.RawMessage),
		done:        make(chan struct{}),
	}

	go hc.readLoop()
	return hc, nil
}

func (h *HubConnection) Invoke(target string, args ...interface{}) (json.RawMessage, error) {
	invID := uuid.New().String()
	resultCh := make(chan json.RawMessage, 1)
	errCh := make(chan error, 1)

	h.mu.Lock()
	h.invocations[invID] = &invocationState{result: resultCh, err: errCh}
	h.mu.Unlock()

	msg := invocationMessage{
		Type:         1,
		InvocationID: invID,
		Target:       target,
		Arguments:    args,
	}
	data, _ := json.Marshal(msg)
	frame := append(data, byte(recordSeparator))

	h.mu.Lock()
	err := h.conn.WriteMessage(websocket.TextMessage, frame)
	h.mu.Unlock()
	if err != nil {
		return nil, fmt.Errorf("invoke write: %w", err)
	}

	select {
	case result := <-resultCh:
		return result, nil
	case err := <-errCh:
		return nil, err
	}
}

func (h *HubConnection) WaitForBroadcast(event string) (json.RawMessage, error) {
	h.mu.Lock()
	if _, ok := h.broadcasts[event]; !ok {
		h.broadcasts[event] = make(chan json.RawMessage, 100)
	}
	ch := h.broadcasts[event]
	h.mu.Unlock()

	select {
	case val := <-ch:
		return val, nil
	case <-h.done:
		return nil, fmt.Errorf("connection closed")
	}
}

func (h *HubConnection) Close() {
	close(h.done)
	h.conn.Close()
}

func (h *HubConnection) readLoop() {
	defer h.Close()
	for {
		_, message, err := h.conn.ReadMessage()
		if err != nil {
			return
		}

		frames := strings.Split(string(message), string(recordSeparator))
		for _, frame := range frames {
			if frame == "" || frame == "{}" {
				continue
			}

			var msg completionMessage
			if err := json.Unmarshal([]byte(frame), &msg); err != nil {
				continue
			}

			switch msg.Type {
			case 1:
				// Server-to-client invocation (broadcast event)
				if msg.Target != nil && msg.Arguments != nil {
					h.mu.Lock()
					ch, ok := h.broadcasts[*msg.Target]
					h.mu.Unlock()
					if ok {
						var val interface{}
						if len(msg.Arguments) == 1 {
							val = msg.Arguments[0]
						} else {
							val = msg.Arguments
						}
						data, _ := json.Marshal(val)
						select {
						case ch <- data:
						default:
						}
					}
				}
			case 3:
				// Completion
				if msg.InvocationID != nil {
					h.mu.Lock()
					state, ok := h.invocations[*msg.InvocationID]
					if ok {
						delete(h.invocations, *msg.InvocationID)
					}
					h.mu.Unlock()

					if ok && state != nil {
						if msg.Error != nil {
							state.err <- fmt.Errorf(*msg.Error)
						} else {
							var result json.RawMessage
							if msg.Result != nil {
								result, _ = json.Marshal(msg.Result)
							}
							state.result <- result
						}
					}
				}
			}
		}
	}
}
