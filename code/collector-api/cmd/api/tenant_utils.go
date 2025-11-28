package main

import "strings"

func isValidTenant(tenantID string) bool {
	if tenantID == "" {
		return false
	}
	for _, allowed := range config.AllowedTenants {
		if strings.TrimSpace(allowed) == tenantID {
			return true
		}
	}
	return false
}
