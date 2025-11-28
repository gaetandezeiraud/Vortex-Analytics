# Vortex Analytics

Vortex Analytics is a turnkey analytics system designed for software applications. Can be launched effortlessly using a simple Docker Compose setup.   
Currently Work In Progress.

## Monorepo Structure

This repository contains the following main projects and folders:

- **code/collector-api/**  
  Go-based API service for collecting and storing tracking events.  
  - Handles event ingestion, geo-IP enrichment, and writes to ClickHouse.
  - Contains all Go source files, Dockerfile, and Go module files.

- **docker-compose.yml**  
  Docker Compose setup for production, including ClickHouse, EchoIP, MaxMind GeoIP updater, and Grafana.


## How to build

```
go build -o bin/vortex-api.exe ./cmd/api
go build -o bin/vortex-migrate.exe ./cmd/migrate
```
