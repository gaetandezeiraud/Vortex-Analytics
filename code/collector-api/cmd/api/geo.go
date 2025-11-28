package main

import (
	"encoding/json"
	"fmt"
	"net"
	"net/http"
	"strings"
)

type GeoInfo struct {
	IP         string  `json:"ip"`
	Country    string  `json:"country"`
	CountryISO string  `json:"country_iso"`
	RegionName string  `json:"region_name"`
	RegionCode string  `json:"region_code"`
	City       string  `json:"city"`
	Latitude   float64 `json:"latitude"`
	Longitude  float64 `json:"longitude"`
}

type GeoService struct {
	EchoIPHost string
	ForceIP    string
}

func NewGeoService(echoHost, forceIP string) *GeoService {
	return &GeoService{
		EchoIPHost: echoHost,
		ForceIP:    forceIP,
	}
}

func (g *GeoService) Resolve(ip string) (*GeoInfo, error) {
	url := fmt.Sprintf("%s/json?ip=%s", g.EchoIPHost, ip)

	req, err := http.NewRequest("GET", url, nil)
	if err != nil {
		return nil, err
	}

	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var info GeoInfo
	err = json.NewDecoder(resp.Body).Decode(&info)
	return &info, err
}

func (g *GeoService) ExtractIP(headers []string, r *http.Request) (net.IP, error) {
	remoteIP := ""

	for _, header := range headers {
		v := r.Header.Get(header)

		if http.CanonicalHeaderKey(header) == "X-Forwarded-For" {
			v = extractFirstForwardedIP(v)
		}

		if v != "" {
			remoteIP = v
			break
		}
	}

	if remoteIP == "" {
		host, _, err := net.SplitHostPort(r.RemoteAddr)
		if err != nil {
			return nil, err
		}
		remoteIP = host
	}

	// If forced IP is set, use it
	if g.ForceIP != "" {
		remoteIP = g.ForceIP
	}

	ip := net.ParseIP(remoteIP)
	if ip == nil {
		return nil, fmt.Errorf("could not parse IP: %s", remoteIP)
	}

	return ip, nil
}

func extractFirstForwardedIP(v string) string {
	if v == "" {
		return ""
	}
	parts := strings.Split(v, ",")
	return strings.TrimSpace(parts[0])
}
