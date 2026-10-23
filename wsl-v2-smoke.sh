#!/usr/bin/env bash
BASE=${BASE:-http://127.0.0.1:5080}
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json')
post() { curl -s -m 25 -X POST "$BASE/api/v3.0/p/agent/$1" "${A[@]}" -d "$2"; }
py() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

echo "===== SCHEMA ====="
post query '{"eql":"SELECT 1"}' >/dev/null
curl -s -m 15 "$BASE/api/v3.0/p/agent/schema" "${A[@]}" | py 'len(json.load(open("/dev/stdin"))["data"])' 2>/dev/null || \
  curl -s -m 15 "$BASE/api/v3.0/p/agent/schema" "${A[@]}" | python3 -c 'import sys,json;print("entities:",len(json.load(sys.stdin)["data"]))'

echo "===== stock_movement.movement_type field present? ====="
curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/stock_movement" "${A[@]}" | python3 -c 'import sys,json; e=json.load(sys.stdin)["data"]; print([f["name"] for f in e["fields"]])'

CID=$(post query '{"eql":"SELECT id FROM customer WHERE name = @n","parameters":[{"name":"n","value":"Delta Office Supplies"}]}' | py 'd["data"]["records"][0]["id"]')
echo "customer_id=$CID"

echo "===== 1. CREATE QUOTE ====="
Q=$(post composed/quote "{\"customer_id\":\"$CID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":3},{\"sku\":\"SKU-2001\",\"quantity\":10,\"discountPercent\":5}],\"valid_until\":\"2026-12-31\"}")
echo "$Q"
QID=$(echo "$Q" | py 'd["data"]["result"]["quote_id"]')

echo "===== 2. CONVERT QUOTE TO ORDER ====="
SO=$(post composed/quote-to-order "{\"quote_id\":\"$QID\"}")
echo "$SO"
OID=$(echo "$SO" | py 'd["data"]["result"]["order_id"]')

echo "===== 3. DELIVER ORDER (DDT) from WH1 ====="
DL=$(post composed/deliver "{\"order_id\":\"$OID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":3},{\"sku\":\"SKU-2001\",\"quantity\":10}],\"warehouse_code\":\"WH1\"}")
echo "$DL"

echo "===== 4. OVER-DELIVERY should FAIL ====="
post composed/deliver "{\"order_id\":\"$OID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":999}],\"warehouse_code\":\"WH1\"}"
echo

echo "===== 5. INVOICE ORDER ====="
INV=$(post composed/invoice "{\"order_id\":\"$OID\",\"due_days\":14}")
echo "$INV"
IID=$(echo "$INV" | py 'd["data"]["result"]["invoice_id"]')
AMT=$(echo "$INV" | py 'd["data"]["result"]["grand_total"]')

echo "===== 6. PARTIAL PAYMENT ====="
HALF=$(python3 -c "print(round($AMT/2,2))")
post composed/payment "{\"invoice_id\":\"$IID\",\"amount\":$HALF,\"method\":\"bank\"}"
echo

echo "===== 7. CREDIT NOTE (reduce balance) ====="
post composed/credit-note "{\"invoice_id\":\"$IID\",\"amount\":50,\"reason\":\"damaged goods\"}"
echo

echo "===== 8. PAY REMAINDER ====="
post composed/payment "{\"invoice_id\":\"$IID\",\"amount\":$HALF,\"method\":\"card\"}"
echo

echo "===== 9. PURCHASE REQUEST -> PO ====="
PR=$(post composed/purchase-request '{"lines":[{"sku":"SKU-1001","quantity":100,"estimatedCost":9.0}],"requested_by":"warehouse"}')
echo "$PR"
PRID=$(echo "$PR" | py 'd["data"]["result"]["request_id"]')
SID=$(post query '{"eql":"SELECT id FROM supplier WHERE name = @n","parameters":[{"name":"n","value":"Prime Coffee Importers"}]}' | py 'd["data"]["records"][0]["id"]')
PO=$(post composed/request-to-po "{\"request_id\":\"$PRID\",\"supplier_id\":\"$SID\"}")
echo "$PO"
POID=$(echo "$PO" | py 'd["data"]["result"]["purchase_order_id"]')

echo "===== 10. RECEIVE PO into WH1 ====="
RC=$(post composed/receive "{\"purchase_order_id\":\"$POID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":100,\"unitCost\":9.0,\"lot\":\"LOT-A\"}],\"warehouse_code\":\"WH1\"}")
echo "$RC"

echo "===== 11. REGISTER PURCHASE INVOICE (triple match) ====="
BILL=$(post composed/purchase-invoice "{\"purchase_order_id\":\"$POID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":100,\"unitCost\":9.0}]}")
echo "$BILL"
BID=$(echo "$BILL" | py 'd["data"]["result"]["bill_id"]')

echo "===== 12. PAY SUPPLIER BILL ====="
post composed/supplier-payment "{\"bill_id\":\"$BID\",\"amount\":990,\"method\":\"bank\"}"
echo

echo "===== 13. TRANSFER STOCK WH1 -> WH2 ====="
post composed/transfer-stock '{"sku":"SKU-1001","from_warehouse":"WH1","to_warehouse":"WH2","quantity":20}'
echo

echo "===== 14. ADJUST STOCK WH2 ====="
post composed/adjust-stock '{"sku":"SKU-1001","warehouse_code":"WH2","new_quantity":50,"reason":"stock count"}'
echo

echo "===== 15. NEGATIVE STOCK BLOCK (deliver more than WH2 has) ====="
post composed/transfer-stock '{"sku":"SKU-1001","from_warehouse":"WH2","to_warehouse":"WH1","quantity":99999}'
echo

echo "===== 16. BLOCKED CUSTOMER ORDER should FAIL ====="
BCID=$(post query '{"eql":"SELECT id FROM customer WHERE name = @n","parameters":[{"name":"n","value":"Suspended Trading Co"}]}' | py 'd["data"]["records"][0]["id"]')
post composed/sales-order "{\"customer_id\":\"$BCID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":1}]}"
echo

echo "===== DONE ====="
