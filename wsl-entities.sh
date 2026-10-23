#!/bin/bash
set -u
BASE=http://127.0.0.1:5080
TOKEN=$(curl -s -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H "Content-Type: application/json" \
  -d '{"email":"erp@webvella.com","password":"erp"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['object'])")
echo "=== non-system entities exposed by the agent API ==="
curl -s "$BASE/api/v3.0/p/agent/schema" -H "Authorization: Bearer $TOKEN" \
  | python3 -c "import sys,json; d=json.load(sys.stdin); print([e['name'] for e in d['data']])"
