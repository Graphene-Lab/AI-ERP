#!/usr/bin/env bash
BASE=${BASE:-http://127.0.0.1:5080}
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json')
post() { curl -s -m 20 -X POST "$BASE/api/v3.0/p/agent/$1" "${A[@]}" -d "$2"; }

CID=$(post query '{"eql":"SELECT id FROM customer WHERE name = @n","parameters":[{"name":"n","value":"Delta Office Supplies"}]}' \
  | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["records"][0]["id"])')
echo "customer_id=$CID"

echo '--- place_sales_order (2 lines, 5% disc on 2nd) ---'
SO=$(post composed/sales-order "{\"customer_id\":\"$CID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":3},{\"sku\":\"SKU-2001\",\"quantity\":10,\"discountPercent\":5}]}")
echo "$SO"
OID=$(echo "$SO" | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["result"]["order_id"])')

echo '--- invoice_sales_order (due 14) ---'
INV=$(post composed/invoice "{\"order_id\":\"$OID\",\"due_days\":14}")
echo "$INV"
IID=$(echo "$INV" | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["result"]["invoice_id"])')
AMT=$(echo "$INV" | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["result"]["amount"])')

echo '--- record_payment partial (half) ---'
HALF=$(python3 -c "print(round($AMT/2,2))")
post composed/payment "{\"invoice_id\":\"$IID\",\"amount\":$HALF,\"method\":\"bank\"}"
echo
echo '--- record_payment remainder -> paid ---'
post composed/payment "{\"invoice_id\":\"$IID\",\"amount\":$HALF,\"method\":\"card\"}"
echo

echo '--- place_purchase_order + receive ---'
SID=$(post query '{"eql":"SELECT id FROM supplier WHERE name = @n","parameters":[{"name":"n","value":"Prime Coffee Importers"}]}' \
  | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["records"][0]["id"])')
PO=$(post composed/purchase-order "{\"supplier_id\":\"$SID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":100,\"unitCost\":9.0}]}")
echo "$PO"
POID=$(echo "$PO" | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["result"]["purchase_order_id"])')
echo 'stock before receive:'
post query '{"eql":"SELECT stock_quantity FROM product WHERE sku = @s","parameters":[{"name":"s","value":"SKU-1001"}]}'
echo
echo 'receive:'
post composed/receive "{\"purchase_order_id\":\"$POID\"}"
echo
echo 'stock after receive:'
post query '{"eql":"SELECT stock_quantity FROM product WHERE sku = @s","parameters":[{"name":"s","value":"SKU-1001"}]}'
echo
