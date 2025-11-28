package shared

import (
	"time"
	"fmt"
)

type TrackingData struct {
	Name       string `json:"name"`
	Value      string `json:"value"`
	Identity   string `json:"identity"`
	SessionID  string `json:"session_id"`
	Platform   string `json:"platform"`
	AppVersion string `json:"app_version"`
}

func (t *TrackingData) Validate() error {
	if t.Name == "" {
		return fmt.Errorf("field 'name' is required")
	}
	if t.Identity == "" {
		return fmt.Errorf("field 'identity' is required")
	}
	if t.SessionID == "" {
		return fmt.Errorf("field 'session_id' is required")
	}
	if t.Platform == "" {
		return fmt.Errorf("field 'platform' is required")
	}
	if t.AppVersion == "" {
		return fmt.Errorf("field 'app_version' is required")
	}
	return nil
}

type Tracking struct {
	TenantID string       `json:"tenant_id"`
	Action   TrackingData `json:"tracking"`
}

type BatchedTracks struct {
	Tracks []Tracking `json:"tracks"`
}

type Event struct {
	ID         int64
	TenantID   string
	SessionID  string
	Name       string
	Value      string
	UserID     string
	AppVersion string
	Platform   string
	Country    string
	Region     string
	Timestamp  time.Time
}
