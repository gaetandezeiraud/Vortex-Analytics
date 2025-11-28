package shared

import (
	"os"
	"strings"

	"github.com/joho/godotenv"
)

type Config struct {
	APIKey             string
	EchoIPHost         string
	ClickHouseHost     string
	ClickHouseDB       string
	ClickHouseUser     string
	ClickHousePassword string
	AllowedTenants     []string
}

func LoadConfig() Config {
	godotenv.Load()
	return Config{
		EchoIPHost:         os.Getenv("ECHOIP_HOST"),
		ClickHouseHost:     os.Getenv("CLICKHOUSE_HOST"),
		ClickHouseDB:       os.Getenv("CLICKHOUSE_DB"),
		ClickHouseUser:     os.Getenv("CLICKHOUSE_USER"),
		ClickHousePassword: os.Getenv("CLICKHOUSE_PASSWORD"),
		AllowedTenants:     strings.Split(os.Getenv("ALLOWED_TENANTS"), ","),
	}
}
