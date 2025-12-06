package main

import (
	"context"
	"fmt"
	"sync"
	"time"
	"vortex/shared"
)

type TrackingWithGeo struct {
	Trk shared.Tracking
	Geo *GeoInfo
}

type Events struct {
	store shared.DataStore
	ch    chan TrackingWithGeo
	buf   []TrackingWithGeo
	mu    sync.Mutex
}

func NewEvents(store shared.DataStore) *Events {
	return &Events{
		store: store,
		ch:    make(chan TrackingWithGeo, 2000),
	}
}

func (e *Events) Add(trk shared.Tracking, geo *GeoInfo) {
	e.ch <- TrackingWithGeo{Trk: trk, Geo: geo}
}

func (e *Events) Run() {
	ticker := time.NewTicker(10 * time.Second)
	defer ticker.Stop()

	for {
		select {
		case item := <-e.ch:
			e.mu.Lock()
			e.buf = append(e.buf, item)
			needsFlush := len(e.buf) >= 15
			e.mu.Unlock()

			if needsFlush {
				e.flush()
			}

		case <-ticker.C:
			e.flush()
		}
	}
}

func (e *Events) flush() {
	e.mu.Lock()
	if len(e.buf) == 0 {
		e.mu.Unlock()
		return
	}

	tmp := e.buf
	e.buf = nil
	e.mu.Unlock()

	batch := make([]shared.EventRecord, 0, len(tmp))

	for _, x := range tmp {
		batch = append(batch, shared.EventRecord{
			TenantID:   x.Trk.TenantID,
			SessionID:  x.Trk.Action.SessionID,
			Name:       x.Trk.Action.Name,
			Value:      x.Trk.Action.Value,
			Identity:   x.Trk.Action.Identity,
			AppVersion: x.Trk.Action.AppVersion,
			Platform:   x.Trk.Action.Platform,
			Country:    x.Geo.Country,
			Region:     x.Geo.RegionName,
		})
	}

	if err := e.store.InsertBatch(context.Background(), batch); err != nil {
		fmt.Println("ERROR inserting batch:", err)
	}
}
