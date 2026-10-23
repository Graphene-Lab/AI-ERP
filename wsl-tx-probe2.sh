#!/usr/bin/env bash
set +e
BASE=http://127.0.0.1:5080
TOKEN=$(curl -s -X POST $BASE/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
echo "token len: ${#TOKEN}"
q() { curl -s -X POST $BASE/api/v3.0/p/agent/query -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "{\"eql\":\"$1\"}"; }
echo "=== customers ==="
q "SELECT * FROM customer"
echo ""
echo "=== products ==="
q "SELECT * FROM product"
echo ""
echo "=== quotes ==="
q "SELECT * FROM quote"
echo ""
