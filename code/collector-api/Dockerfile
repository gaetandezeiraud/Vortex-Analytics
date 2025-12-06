# ----------------------------
# Stage 1: Build
# ----------------------------
FROM golang:1.25.5-alpine AS build

WORKDIR /app

# Modules
COPY go.mod go.sum ./
RUN go mod download

# Source code
COPY ./cmd/api/*.go ./cmd/api/
COPY ./cmd/migrate/*.go ./cmd/migrate/
COPY ./shared ./shared/

# Build args
ARG VERSION=dev

# Build API
RUN go build -ldflags "-X main.version=${VERSION}" -o vortex-api ./cmd/api

# Build Migrate
RUN go build -ldflags "-X main.version=${VERSION}" -o migrate ./cmd/migrate

# ----------------------------
# Stage 2: Minimal image
# ----------------------------
FROM alpine:3.18 AS runtime
RUN apk add --no-cache ca-certificates

WORKDIR /opt/vortex

# Copy binaries
COPY --from=build /app/vortex-api ./vortex-api
COPY --from=build /app/migrate ./migrate

EXPOSE 9876

# Default command (API)
CMD ["./vortex-api"]
