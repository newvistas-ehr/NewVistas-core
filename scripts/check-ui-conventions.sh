#!/usr/bin/env bash
#
# UI convention drift guard for the main Blazor app (NewVistas.BlazorWeb).
#
# Fails (exit 1) if any pre-canonical / Bootstrap idiom reappears in a page under
# Components/Pages. The canonical convention is documented in
#   NewVistas.BlazorWeb/UI-CONVENTIONS.md
# and the shared components + single source-of-truth CSS live in
#   NewVistas.BlazorWeb/Components/Shared/  and  NewVistas.BlazorWeb/wwwroot/app.css
#
# Out of scope (NOT scanned):
#   - PatientPortal.razor  (patient-facing UI; its own design treatment)
#   - Login.razor          (pre-auth page; intentionally distinct)
#   - The WPF CharUI / WpfDelphiUI families (separate projects, deliberate
#     throwbacks to the VistA character UI and CPRS/RPMS Delphi front ends).
#
# To grant a deliberate, reviewed exception, add the file to EXEMPT below.

set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PAGES="$ROOT/NewVistas.BlazorWeb/Components/Pages"
EXEMPT='PatientPortal\.razor|Login\.razor'

if [ ! -d "$PAGES" ]; then
  echo "check-ui-conventions: pages directory not found: $PAGES" >&2
  exit 2
fi

# Each entry: "<extended-regex>@@<why / canonical replacement>"
PATTERNS=(
  'tab-btn@@use canonical .tab / .tab-bar (nested sub-tabs: name them .sub-tab)'
  'nav-link@@use canonical .tab'
  'nav-tabs@@use canonical .tab-bar'
  'btn-success@@use btn btn-primary'
  'btn-outline@@use btn btn-primary / btn btn-secondary'
  'btn-danger@@use btn btn-primary (no danger variant in the convention)'
  'btn-warning@@use btn btn-primary'
  'btn-info"@@use btn btn-primary'
  'alert-danger@@use alert-error'
  'form-control@@use form-input'
  'form-select@@use form-input'
  'class="btn-primary[ "]@@standalone btn-primary -> class="btn btn-primary"'
  'table table-@@use class="data-table"'
  'table-striped@@use class="data-table"'
  'table-bordered@@use class="data-table"'
  'table-hover@@use class="data-table"'
  'badge bg-@@use canonical badge badge-{danger,warning,info,success,neutral}'
  '#1a1a2e@@dark-theme residue -> light canonical palette (see AccountsReceivable.razor)'
  '#16213e@@dark-theme residue -> light canonical palette'
  '#0d1117@@dark-theme residue -> light canonical palette'
  '#0f1419@@dark-theme residue -> light canonical palette'
)

fail=0
for entry in "${PATTERNS[@]}"; do
  pat="${entry%%@@*}"
  why="${entry##*@@}"
  hits="$(grep -rInE "$pat" "$PAGES" --include='*.razor' 2>/dev/null | grep -vE "(${EXEMPT}):" || true)"
  if [ -n "$hits" ]; then
    fail=1
    echo "✗ off-convention marker  [$pat]  -> $why"
    printf '%s\n' "$hits" | sed 's#'"$ROOT"'/##; s/^/      /'
    echo
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "UI convention drift detected in NewVistas.BlazorWeb/Components/Pages."
  echo "Fix the page(s) to the canonical idiom (NewVistas.BlazorWeb/UI-CONVENTIONS.md),"
  echo "or, for a deliberate reviewed exception, add the file to EXEMPT in scripts/check-ui-conventions.sh."
  exit 1
fi

count="$(find "$PAGES" -name '*.razor' | wc -l | tr -d ' ')"
echo "✓ UI conventions OK — scanned $count page(s) in NewVistas.BlazorWeb/Components/Pages; no off-convention markers."
