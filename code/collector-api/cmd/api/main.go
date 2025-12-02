package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"net/http"

	"vortex/shared"

	"github.com/rs/cors"
)

var (
	forceIP string
	config  shared.Config
	events  *Events
	geoSvc  *GeoService
)

var version = "dev"

func main() {
	fmt.Println("Start vortex...")

	flag.StringVar(&forceIP, "ip", "", "Force IP for request, useful in local dev")
	flag.Parse()

	// Load config
	config = shared.LoadConfig()
	fmt.Println("Allowed tenants:", config.AllowedTenants)

	// Init Geo service
	geoSvc = NewGeoService(config.EchoIPHost, forceIP)

	// Init ClickHouse datastore
	store := &shared.ClickHouseStore{}
	if err := store.Open(
		config.ClickHouseHost,
		config.ClickHouseDB,
		config.ClickHouseUser,
		config.ClickHousePassword,
	); err != nil {
		log.Fatal("ClickHouse connection error: ", err)
	}

	// Start event batching processor
	events = NewEvents(store)
	go events.Run()

	// HTTP routes
	mux := http.NewServeMux()
	mux.HandleFunc("/", index)
	mux.HandleFunc("/health", health)
	mux.HandleFunc("/track", track)
	mux.HandleFunc("/batch", trackBatch)

	// CORS support
	handler := cors.Default().Handler(mux)

	log.Println("Listening on :9876")
	log.Fatal(http.ListenAndServe(":9876", handler))
}

// Handlers
func index(w http.ResponseWriter, _ *http.Request) {
	fmt.Fprintf(w, "Vortex collector-api %s", version)
}

func health(w http.ResponseWriter, r *http.Request) {
	w.WriteHeader(http.StatusOK)
}

func track(w http.ResponseWriter, r *http.Request) {
	if !isJSONRequest(w, r) {
		return
	}

	body, err := readBody(w, r)
	if err != nil {
		return
	}

	var trk shared.Tracking
	if err := json.Unmarshal(body, &trk); err != nil {
		http.Error(w, "Invalid JSON body", http.StatusBadRequest)
		return
	}

	if err := trk.Action.Validate(); err != nil {
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}

	if !isValidTenant(trk.TenantID) {
		http.Error(w, "Invalid or unauthorized tenant", http.StatusForbidden)
		return
	}

	// Extract IP
	ip, err := geoSvc.ExtractIP([]string{"X-Forwarded-For", "X-Real-IP"}, r)
	if err != nil {
		log.Println("error extracting IP:", err)
		return
	}

	// Resolve geo
	geo, err := geoSvc.Resolve(ip.String())
	if err != nil {
		log.Println("error resolving GeoIP:", err)
		return
	}

	// Deterministic session ID
	trk.Action.SessionID = shared.DeterministicGUID(trk.Action.Identity, trk.Action.SessionID)

	events.Add(trk, geo)
	w.WriteHeader(http.StatusOK)
}

func trackBatch(w http.ResponseWriter, r *http.Request) {
	if !isJSONRequest(w, r) {
		return
	}

	body, err := readBody(w, r)
	if err != nil {
		return
	}

	var batch shared.BatchedTracks
	if err := json.Unmarshal(body, &batch); err != nil {
		http.Error(w, "Invalid JSON body", http.StatusBadRequest)
		return
	}

	ip, err := geoSvc.ExtractIP([]string{"X-Forwarded-For", "X-Real-IP"}, r)
	if err != nil {
		log.Println("error extracting IP:", err)
		return
	}

	geo, err := geoSvc.Resolve(ip.String())
	if err != nil {
		log.Println("error resolving GeoIP:", err)
		return
	}

	for _, trk := range batch.Tracks {
		if !isValidTenant(trk.TenantID) {
			log.Println("Skipping invalid tenant:", trk.TenantID)
			continue
		}

		if err := trk.Action.Validate(); err != nil {
			log.Println("Skipping invalid track:", err)
			continue
		}

		trk.Action.SessionID = shared.DeterministicGUID(trk.Action.Identity, trk.Action.SessionID)

		events.Add(trk, geo)
	}

	w.WriteHeader(http.StatusOK)
}
