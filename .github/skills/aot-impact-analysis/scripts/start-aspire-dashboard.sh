#!/usr/bin/env bash
# Starts a standalone Aspire Dashboard to receive OpenTelemetry (OTLP) data from the .NET CLI, and
# prints the OTLP endpoint + UI login URL that measure-aot-startup.sh / `aspire otel` consume.
#
# Prefers the `aspire` CLI (`aspire dashboard run`); otherwise falls back to the official container
# image (mcr.microsoft.com/dotnet/aspire-dashboard). Either way it exposes:
#   * UI            http://localhost:18888
#   * OTLP/gRPC     http://localhost:4317   (point the CLI's OTEL_EXPORTER_OTLP_ENDPOINT here)
#   * OTLP/HTTP     http://localhost:4318
#
# Run in its own terminal (it blocks) or as a background process. With the `aspire` CLI, copy the
# printed http://localhost:18888/login?t=<token> URL and pass it to `aspire otel --dashboard-url`.
#
# The PowerShell sibling Start-AspireDashboard.ps1 is the canonical implementation.
#
# Usage:
#   scripts/start-aspire-dashboard.sh [--use-docker] [--otlp-grpc-port <n>] [--ui-port <n>]
set -euo pipefail

use_docker=0
otlp_grpc_port=4317
ui_port=18888

usage() { awk 'NR==1{next} /^#/{sub(/^# ?/,"");print;next} {exit}' "$0"; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --use-docker) use_docker=1; shift;;
    --otlp-grpc-port) otlp_grpc_port="$2"; shift 2;;
    --ui-port) ui_port="$2"; shift 2;;
    -h|--help) usage; exit 0;;
    *) echo "Unknown option: $1" >&2; usage; exit 1;;
  esac
done

if command -v aspire >/dev/null 2>&1 && [[ "$use_docker" -eq 0 ]]; then
  echo "==> Starting Aspire Dashboard via the aspire CLI..."
  echo "    UI:        http://localhost:${ui_port}"
  echo "    OTLP/gRPC: http://localhost:${otlp_grpc_port}"
  echo "    Copy the printed login URL (with ?t=<token>) for 'aspire otel --dashboard-url'."
  echo ""
  exec aspire dashboard run --frontend-url "http://localhost:${ui_port}"
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Neither the 'aspire' CLI nor 'docker' is available. Install the Aspire CLI (dotnet tool install -g aspire.cli) or Docker." >&2
  exit 1
fi

echo "==> Starting Aspire Dashboard via container image..."
echo "    UI:        http://localhost:${ui_port}"
echo "    OTLP/gRPC: http://localhost:${otlp_grpc_port}"
echo "    Anonymous access is enabled for local dev (no token required)."
echo ""
exec docker run --rm -it \
  -p "${ui_port}:18888" \
  -p "${otlp_grpc_port}:18889" \
  -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
  --name aspire-dashboard \
  mcr.microsoft.com/dotnet/aspire-dashboard:latest
