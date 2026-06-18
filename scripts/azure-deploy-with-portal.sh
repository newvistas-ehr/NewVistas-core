#!/usr/bin/env bash
# =============================================================================
# azure-deploy-with-portal.sh — Deploy NewVistas WITH the Patient Portal.
#
# Thin convenience wrapper. Identical to running:
#   ./scripts/azure-deploy.sh --with-portal
#
# All the real logic lives in azure-deploy.sh (single source of truth); this
# just sets the --with-portal flag and forwards any other arguments.
#
# Usage (from the repository root):
#   ./scripts/azure-deploy-with-portal.sh
# =============================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$SCRIPT_DIR/azure-deploy.sh" --with-portal "$@"
