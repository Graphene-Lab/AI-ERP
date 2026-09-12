#!/usr/bin/env bash
# Smoke test of the AgentApi surface against a running AI.Erp.Site.
BASE=${BASE:-http://127.0.0.1:5080}
EMAIL=${EMAIL:-erp@webvella.com}
PASS=${PASS:-erp}

TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" \
  | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')

echo "TOKEN len: ${#TOKEN}"

echo '--- schema (all) ---'
curl -s -m 15 "$BASE/api/v3.0/p/agent/schema" -H "Authorization: Bearer $TOKEN" \
  | python3 -c 'import sys,json; d=json.load(sys.stdin); print("success:",d["success"]); ents=d.get("data") or []; print("entities:",[e["name"] for e in ents])'

echo '--- query users ---'
curl -s -m 15 -X POST "$BASE/api/v3.0/p/agent/query" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"eql":"SELECT id, username, email FROM user WHERE username = @u","parameters":[{"name":"u","value":"administrator"}]}' \
  | head -c 400; echo
