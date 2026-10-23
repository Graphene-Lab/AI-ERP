#!/usr/bin/env bash
BASE=${BASE:-http://127.0.0.1:5080}
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json')
q() { curl -s -m 20 -X POST "$BASE/api/v3.0/p/agent/query" "${A[@]}" -d "$1" | python3 -c 'import sys,json; d=json.load(sys.stdin); print(json.dumps(d.get("data",{}).get("records",[]),ensure_ascii=False))'; }

echo "=== customers (name, blocked, category) ==="
q '{"eql":"SELECT name, blocked, category FROM customer ORDER BY name"}'
echo "=== suppliers (name, blocked) ==="
q '{"eql":"SELECT name, blocked FROM supplier ORDER BY name"}'
echo "=== warehouses (code, is_default, allow_negative_stock) ==="
q '{"eql":"SELECT code, is_default, allow_negative_stock FROM warehouse ORDER BY code"}'
echo "=== product fields (schema) ==="
curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/product" "${A[@]}" | python3 -c 'import sys,json; e=json.load(sys.stdin)["data"]; print([f["name"] for f in e["fields"]])'
echo "=== products (sku, stock_quantity, min_stock) ==="
q '{"eql":"SELECT sku, stock_quantity, min_stock FROM product ORDER BY sku"}'
echo "=== vat_codes ==="
q '{"eql":"SELECT code, rate FROM vat_code ORDER BY code"}'
echo "=== payment_terms ==="
q '{"eql":"SELECT code, days FROM payment_term ORDER BY code"}'
