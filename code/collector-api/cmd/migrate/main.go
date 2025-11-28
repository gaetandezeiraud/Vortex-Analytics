package main

import (
	"context"
	"log"
	"time"
	"vortex/shared"
)

func main() {
	// Load configuration
	config := shared.LoadConfig()

	// Init ClickHouse store
	store := &shared.ClickHouseStore{}
	var err error

	// Retry logic for DB connection
	for i := 0; i < 10; i++ {
		err = store.Open(
			config.ClickHouseHost,
			config.ClickHouseDB,
			config.ClickHouseUser,
			config.ClickHousePassword,
		)
		if err == nil {
			break
		}
		log.Println("Failed to connect to ClickHouse, retrying...", err)
		time.Sleep(2 * time.Second)
	}

	if err != nil {
		log.Fatalf("Could not connect to ClickHouse: %v", err)
	}

	log.Println("Connected to ClickHouse.")

	// Ensure table exists
	if err := ensureTable(store); err != nil {
		log.Fatalf("Could not create table: %v", err)
	}

	log.Println("Migration completed successfully.")
}

// EnsureTable creates the events table if it does not exist
func ensureTable(store *shared.ClickHouseStore) error {
	qry := `
	CREATE TABLE IF NOT EXISTS events (
		tenant_id String NOT NULL,
		session_id String NOT NULL,
		name String NOT NULL,
		value JSON,
		identity String NOT NULL,
		app_version String,
		platform String,
		country String NOT NULL,
		region String NOT NULL,
		timestamp DateTime DEFAULT now()
	)
	ENGINE = MergeTree()
	PARTITION BY toYYYYMMDD(timestamp)
	ORDER BY (tenant_id, timestamp);
	`

	ctx := context.Background()
	return store.Exec(ctx, qry)
}
