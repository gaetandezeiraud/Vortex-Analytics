package shared

import (
	"context"
	"log"

	"github.com/ClickHouse/clickhouse-go/v2"
)

type ClickHouseStore struct {
	DB clickhouse.Conn
}

func (c *ClickHouseStore) Open(host, db, user, pass string) error {
	conn, err := clickhouse.Open(&clickhouse.Options{
		Addr: []string{host},
		Auth: clickhouse.Auth{
			Database: db,
			Username: user,
			Password: pass,
		},
		Debug: false,
	})
	if err != nil {
		return err
	}

	if err := conn.Ping(context.Background()); err != nil {
		return err
	}

	c.DB = conn
	return nil
}

func (c *ClickHouseStore) Exec(ctx context.Context, query string) error {
	return c.DB.Exec(ctx, query)
}

func (c *ClickHouseStore) InsertBatch(ctx context.Context, records []EventRecord) error {
	qry := `
		INSERT INTO events
		(
			tenant_id, session_id, name, value, identity, 
			app_version, platform, country, region, timestamp
		) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
	`

	batch, err := c.DB.PrepareBatch(ctx, qry)
	if err != nil {
		return err
	}

	for _, r := range records {
		var val any
		if r.Value == "" {
			val = "{}" // Can't be nil yet, see https://github.com/ClickHouse/clickhouse-go/issues/1707
		} else {
			val = r.Value
		}

		err = batch.Append(
			r.TenantID,
			r.SessionID,
			r.Name,
			val,
			r.Identity,
			r.AppVersion,
			r.Platform,
			r.Country,
			r.Region,
			r.Timestamp,
		)
		if err != nil {
			return err
		}
	}

	if err := batch.Send(); err != nil {
        log.Println("batch send failed:", err)
		return err
    }

    return nil
}
