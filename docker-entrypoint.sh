#!/usr/bin/env bash
# Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
#
# All-in-one launcher: runs the Orleans silo, the REST API/WebServer, and the
# Blazor Server UI as three cooperating processes inside a single container.
# Intended for self-contained demos — grain storage is in-memory and demo data
# is seeded on first run, so no external database is required.
set -uo pipefail

SILO_DIR=/app/silo
WEBSERVER_DIR=/app/webserver
BLAZOR_DIR=/app/blazor

# Container-internal port layout. 8080/8081 are published; the Orleans silo
# (11111) and client gateway (30000) are only used between the processes here.
UI_PORT=8080
API_PORT=8081
GATEWAY_PORT=30000

pids=()

shutdown() {
  echo "[entrypoint] Shutting down NewVistas..."
  if [ ${#pids[@]} -gt 0 ]; then
    for pid in "${pids[@]}"; do
      kill -TERM "$pid" 2>/dev/null || true
    done
  fi
  wait 2>/dev/null || true
  exit 0
}
trap shutdown SIGTERM SIGINT

echo "[entrypoint] Starting Orleans silo..."
( cd "$SILO_DIR" && exec dotnet NewVistas.SiloHost.dll ) &
pids+=("$!")

echo "[entrypoint] Waiting for the Orleans gateway on 127.0.0.1:${GATEWAY_PORT}..."
for _ in $(seq 1 90); do
  if (exec 3<>"/dev/tcp/127.0.0.1/${GATEWAY_PORT}") 2>/dev/null; then
    exec 3>&- 3<&- || true
    echo "[entrypoint] Orleans gateway is accepting connections."
    break
  fi
  # Bail out early if the silo died during startup.
  if ! kill -0 "${pids[0]}" 2>/dev/null; then
    echo "[entrypoint] Silo process exited during startup." >&2
    shutdown
  fi
  sleep 1
done

echo "[entrypoint] Starting WebServer (REST API + Identity) on :${API_PORT}..."
( cd "$WEBSERVER_DIR" && exec dotnet NewVistas.WebServer.dll --urls "http://0.0.0.0:${API_PORT}" ) &
pids+=("$!")

echo "[entrypoint] Starting Blazor Server UI on :${UI_PORT}..."
( cd "$BLAZOR_DIR" && exec dotnet NewVistas.BlazorWeb.dll --urls "http://0.0.0.0:${UI_PORT}" ) &
pids+=("$!")

echo "[entrypoint] All services started. UI: http://localhost:${UI_PORT}  API: http://localhost:${API_PORT}"

# If any one of the three services exits, tear the whole container down so the
# orchestrator (or `docker run`) sees the failure and can restart it cleanly.
wait -n
echo "[entrypoint] A NewVistas service exited; shutting the container down." >&2
shutdown
