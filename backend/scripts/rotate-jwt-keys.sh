#!/usr/bin/env bash
set -euo pipefail

# Rotates RS256 keys in a Jwt__SigningKeys JSON secret file (BE-SEC-10).
# Requires: openssl, python3 (used strictly for canonical JSON & RFC 7638 kid thumbprint).
#
# Usage:
#   ./rotate-jwt-keys.sh /run/secrets/Jwt__SigningKeys [--immediate]

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <path-to-Jwt__SigningKeys> [--immediate]" >&2
  exit 1
fi

TARGET_PATH="$1"
IMMEDIATE="0"
if [[ "${2:-}" == "--immediate" ]]; then
  IMMEDIATE="1"
fi

PRE_PUBLISH_HOURS="${PRE_PUBLISH_HOURS:-24}"
VALID_DAYS="${VALID_DAYS:-104}"
PRUNE_GRACE_HOURS="${PRUNE_GRACE_HOURS:-1}"
KEY_BITS="${KEY_BITS:-3072}"

if [[ "$KEY_BITS" -lt 2048 ]]; then
  echo "KEY_BITS must be >= 2048" >&2
  exit 1
fi

TMP_PEM="$(mktemp)"
TMP_JSON="$(mktemp)"
trap 'rm -f "$TMP_PEM" "$TMP_JSON"' EXIT

openssl genpkey -algorithm RSA -pkeyopt "rsa_keygen_bits:${KEY_BITS}" -out "$TMP_PEM" 2>/dev/null

python3 - "$TARGET_PATH" "$TMP_PEM" "$TMP_JSON" "$IMMEDIATE" "$PRE_PUBLISH_HOURS" "$VALID_DAYS" "$PRUNE_GRACE_HOURS" <<'PY'
import base64
import datetime as dt
import hashlib
import json
import os
import subprocess
import sys

target_path, pem_path, out_path, immediate, pre_h, valid_d, grace_h = sys.argv[1:8]
pre_h = int(pre_h)
valid_d = int(valid_d)
grace_h = int(grace_h)

now = dt.datetime.now(dt.timezone.utc)
not_before = (now - dt.timedelta(minutes=1)) if immediate == "1" else (now + dt.timedelta(hours=pre_h))
not_after = not_before + dt.timedelta(days=valid_d)

with open(pem_path, "r", encoding="utf-8") as f:
    new_pem = f.read()

# Extract RSA modulus and exponent using openssl text output to compute RFC 7638 kid
rsa_text = subprocess.check_output(["openssl", "rsa", "-in", pem_path, "-noout", "-modulus"], text=True)
modulus_hex = rsa_text.strip().split("=")[1]
modulus_bytes = bytes.fromhex(modulus_hex)
exponent_bytes = b"\x01\x00\x01"  # OpenSSL default 65537 (AQAB)

def b64url(b: bytes) -> str:
    return base64.urlsafe_b64encode(b).rstrip(b"=").decode("ascii")

canonical = json.dumps({"e": b64url(exponent_bytes), "kty": "RSA", "n": b64url(modulus_bytes)}, separators=(",", ":"))
kid = b64url(hashlib.sha256(canonical.encode("utf-8")).digest())

kept = []
pruned = 0
if os.path.exists(target_path):
    with open(target_path, "r", encoding="utf-8") as f:
        raw = f.read().strip()
    if raw:
        existing = json.loads(raw).get("keys", [])
        for entry in existing:
            exp = dt.datetime.fromisoformat(entry["notAfter"].replace("Z", "+00:00"))
            if exp + dt.timedelta(hours=grace_h) > now:
                kept.append(entry)
            else:
                pruned += 1

kept.append({
    "privateKeyPem": new_pem,
    "notBefore": not_before.isoformat().replace("+00:00", "Z"),
    "notAfter": not_after.isoformat().replace("+00:00", "Z"),
})

with open(out_path, "w", encoding="utf-8") as f:
    json.dump({"keys": kept}, f, indent=2)

print(f"Added key kid={kid} (notBefore={kept[-1]['notBefore']}, notAfter={kept[-1]['notAfter']}). Retained={len(kept)}, Pruned={pruned}.")
PY

chmod 600 "$TMP_JSON"
mkdir -p "$(dirname "$TARGET_PATH")"
mv -f "$TMP_JSON" "$TARGET_PATH"
