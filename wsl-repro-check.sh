#!/usr/bin/env bash
base=http://127.0.0.1:5080
token=$(curl -s -X POST "$base/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
echo "=== reprovision response ==="
curl -s -X POST "$base/api/v3.0/p/agent/reprovision" -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d '{}'
echo ""
echo "=== setup-status after ==="
curl -s "$base/api/v3.0/p/agent/setup-status" -H "Authorization: Bearer $token"
echo ""
echo "=== company rows in DB ==="
export PGPASSWORD=dev
psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT name, legal_name, vat_number, currency FROM rec_company;"
