#!/usr/bin/env bash
BASE=${BASE:-http://127.0.0.1:5080}
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json')
post() { curl -s -m 25 -X POST "$BASE/api/v3.0/p/agent/$1" "${A[@]}" -d "$2"; }
py() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

CID=$(post query '{"eql":"SELECT id FROM customer WHERE name = @n","parameters":[{"name":"n","value":"Evergreen Grocers"}]}' | py 'd["data"]["records"][0]["id"]')
echo "Evergreen CID=$CID"

echo "=== create order (SKU-1001 x2) ==="
SO=$(post composed/sales-order "{\"customer_id\":\"$CID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":2}]}")
echo "$SO"
OID=$(echo "$SO" | py 'd["data"]["result"]["order_id"]')
echo "OID=$OID"

echo "=== invoice that order ==="
INV=$(post composed/invoice "{\"order_id\":\"$OID\",\"due_days\":30}")
echo "$INV"

echo "=== invoice count now ==="
post query '{"eql":"SELECT id FROM invoice"}' | py 'd["data"]["total_count"]'
