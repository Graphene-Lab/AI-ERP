#!/usr/bin/env bash
BASE=${BASE:-http://127.0.0.1:5080}
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')

q() { curl -s -m 15 -X POST "$BASE/api/v3.0/p/agent/query" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "{\"eql\":\"$1\"}"; }

echo "company: $(q 'SELECT name, city, currency FROM company' | python3 -c 'import sys,json;d=json.load(sys.stdin);print(d["data"]["total_count"], d["data"]["records"])')"
echo "customers: $(q 'SELECT name, category FROM customer' | python3 -c 'import sys,json;d=json.load(sys.stdin);print(d["data"]["total_count"])')"
echo "products: $(q 'SELECT sku, name, unit_price, stock_quantity FROM product' | python3 -c 'import sys,json;d=json.load(sys.stdin);print(d["data"]["total_count"])')"
echo "suppliers: $(q 'SELECT name FROM supplier' | python3 -c 'import sys,json;d=json.load(sys.stdin);print(d["data"]["total_count"])')"
echo "one product detail:"
q "SELECT id, sku, name, unit_price, unit_cost, stock_quantity FROM product WHERE sku = @s" | head -c 200
echo
echo "-- by param --"
curl -s -m 15 -X POST "$BASE/api/v3.0/p/agent/query" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"eql":"SELECT id, sku, unit_price FROM product WHERE sku = @s","parameters":[{"name":"s","value":"SKU-1001"}]}' | head -c 300
echo
