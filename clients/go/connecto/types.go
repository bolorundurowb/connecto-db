package connecto

import "time"

type UserRes struct {
	ID        string    `json:"id"`
	FirstName *string   `json:"firstName"`
	LastName  *string   `json:"lastName"`
	Username  string    `json:"username"`
	CreatedAt time.Time `json:"createdAt"`
}

type AuthRes struct {
	User      UserRes   `json:"user"`
	Token     string    `json:"token"`
	ExpiresAt time.Time `json:"expiresAt"`
}

type GenericRes struct {
	Message string `json:"message"`
}

type LoginReq struct {
	Username string `json:"username"`
	Password string `json:"password"`
}

type RegisterReq struct {
	Username  string  `json:"username"`
	Password  string  `json:"password"`
	FirstName *string `json:"firstName,omitempty"`
	LastName  *string `json:"lastName,omitempty"`
}

type FlexMap map[string]interface{}

// SignalR protocol messages

type handshakeRequest struct {
	Protocol string `json:"protocol"`
	Version  int    `json:"version"`
}

type invocationMessage struct {
	Type         int           `json:"type"`
	InvocationID string        `json:"invocationId"`
	Target       string        `json:"target"`
	Arguments    []interface{} `json:"arguments"`
}

type completionMessage struct {
	Type         int           `json:"type"`
	InvocationID *string       `json:"invocationId,omitempty"`
	Result       interface{}   `json:"result,omitempty"`
	Error        *string       `json:"error,omitempty"`
	Target       *string       `json:"target,omitempty"`
	Arguments    []interface{} `json:"arguments,omitempty"`
}
