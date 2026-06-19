package connecto

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
)

type Client struct {
	BaseURL       string
	http          *http.Client
	token         string
	dataHub       *HubConnection
	collectionHub *HubConnection
}

func NewClient(url string) *Client {
	return &Client{
		BaseURL: url,
		http:    &http.Client{},
	}
}

func (c *Client) IsConnected() bool {
	return c.dataHub != nil && c.collectionHub != nil
}

// ── Auth ────────────────────────────────────────────────────────

func (c *Client) Login(username, password string) (*AuthRes, error) {
	req := LoginReq{Username: username, Password: password}
	auth, err := post[AuthRes](c.http, c.BaseURL+"/api/auth/login", req)
	if err != nil {
		return nil, err
	}
	c.token = auth.Token
	return auth, nil
}

func (c *Client) Register(username, password string, firstName, lastName *string) (*AuthRes, error) {
	req := RegisterReq{
		Username:  username,
		Password:  password,
		FirstName: firstName,
		LastName:  lastName,
	}
	auth, err := post[AuthRes](c.http, c.BaseURL+"/api/auth/register", req)
	if err != nil {
		return nil, err
	}
	c.token = auth.Token
	return auth, nil
}

// ── Connect ─────────────────────────────────────────────────────

func (c *Client) Connect() error {
	if c.token == "" {
		return fmt.Errorf("not authenticated")
	}
	var err error
	c.dataHub, err = NewHubConnection(c.BaseURL, "/data-stream", c.token)
	if err != nil {
		return fmt.Errorf("data hub: %w", err)
	}
	c.collectionHub, err = NewHubConnection(c.BaseURL, "/collection-stream", c.token)
	if err != nil {
		return fmt.Errorf("collection hub: %w", err)
	}
	return nil
}

func (c *Client) Disconnect() {
	if c.dataHub != nil {
		c.dataHub.Close()
	}
	if c.collectionHub != nil {
		c.collectionHub.Close()
	}
}

// ── Collections ─────────────────────────────────────────────────

func (c *Client) ListTables() ([]string, error) {
	result, err := c.collectionHub.Invoke("ListTables")
	if err != nil {
		return nil, err
	}
	var tables []string
	json.Unmarshal(result, &tables)
	return tables, nil
}

func (c *Client) CreateTable(name string) error {
	_, err := c.collectionHub.Invoke("CreateTable", name)
	return err
}

func (c *Client) DeleteTable(name string) error {
	_, err := c.collectionHub.Invoke("DeleteTable", name)
	return err
}

// ── Data ────────────────────────────────────────────────────────

func (c *Client) Subscribe(table string) error {
	_, err := c.dataHub.Invoke("SubscribeToTable", table)
	return err
}

func (c *Client) Unsubscribe(table string) error {
	_, err := c.dataHub.Invoke("UnsubscribeFromTable", table)
	return err
}

func (c *Client) GetAllRecords(table string) ([]FlexMap, error) {
	result, err := c.dataHub.Invoke("GetAllRecords", table)
	if err != nil {
		return nil, err
	}
	var records []FlexMap
	json.Unmarshal(result, &records)
	return records, nil
}

func (c *Client) GetRecord(table, id string) (FlexMap, error) {
	result, err := c.dataHub.Invoke("GetRecord", table, id)
	if err != nil {
		return nil, err
	}
	var record FlexMap
	json.Unmarshal(result, &record)
	return record, nil
}

func (c *Client) Upsert(table string, data FlexMap) (FlexMap, error) {
	result, err := c.dataHub.Invoke("UpsertDataRecord", table, data)
	if err != nil {
		return nil, err
	}
	var record FlexMap
	json.Unmarshal(result, &record)
	return record, nil
}

func (c *Client) DeleteRecord(table, id string) error {
	_, err := c.dataHub.Invoke("DeleteRecord", table, id)
	return err
}

// ── Realtime Listeners ──────────────────────────────────────────

func (c *Client) OnEntityCreated(cb func(string, FlexMap)) {
	go func() {
		for {
			val, err := c.dataHub.WaitForBroadcast("EntityCreated")
			if err != nil {
				return
			}
			// Broadcast args are [tableName, entity]
			var args []json.RawMessage
			json.Unmarshal(val, &args)
			if len(args) >= 2 {
				var table string
				var entity FlexMap
				json.Unmarshal(args[0], &table)
				json.Unmarshal(args[1], &entity)
				cb(table, entity)
			}
		}
	}()
}

func (c *Client) OnEntityUpdated(cb func(string, FlexMap)) {
	go func() {
		for {
			val, err := c.dataHub.WaitForBroadcast("EntityUpdated")
			if err != nil {
				return
			}
			var args []json.RawMessage
			json.Unmarshal(val, &args)
			if len(args) >= 2 {
				var table string
				var entity FlexMap
				json.Unmarshal(args[0], &table)
				json.Unmarshal(args[1], &entity)
				cb(table, entity)
			}
		}
	}()
}

func (c *Client) OnTableCreated(cb func(string)) {
	go func() {
		for {
			val, err := c.collectionHub.WaitForBroadcast("TableCreated")
			if err != nil {
				return
			}
			var table string
			json.Unmarshal(val, &table)
			cb(table)
		}
	}()
}

func (c *Client) OnTableDeleted(cb func(string)) {
	go func() {
		for {
			val, err := c.collectionHub.WaitForBroadcast("TableDeleted")
			if err != nil {
				return
			}
			var table string
			json.Unmarshal(val, &table)
			cb(table)
		}
	}()
}

// ── Helpers ─────────────────────────────────────────────────────

func post[T any](client *http.Client, url string, body interface{}) (*T, error) {
	data, _ := json.Marshal(body)
	resp, err := client.Post(url, "application/json", bytes.NewReader(data))
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var result T
	if resp.StatusCode >= 400 {
		var e GenericRes
		json.NewDecoder(resp.Body).Decode(&e)
		return nil, fmt.Errorf(e.Message)
	}

	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return nil, err
	}
	return &result, nil
}
