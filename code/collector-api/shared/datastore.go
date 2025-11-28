package shared

import (
	"context"
)

type EventRecord struct {
	ID         int64
	TenantID   string
	SessionID  string
	Name       string
	Value      string
	Identity   string
	AppVersion string
	Platform   string
	Country    string
	Region     string
}

type DataStore interface {
	Open(host, db, user, pass string) error
	InsertBatch(ctx context.Context, records []EventRecord) error
}
