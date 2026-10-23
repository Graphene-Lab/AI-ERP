#!/usr/bin/env bash
# v3 composed-operations smoke test (deterministic, no LLM).
# Exercises each new composed op against a freshly-bootstrapped v3 database.
BASE=${BASE:-http://127.0.0.1:5080}
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json')
post() { curl -s -m 25 -X POST "$BASE/api/v3.0/p/agent/$1" "${A[@]}" -d "$2"; }
py() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

pass=0; fail=0
check() { if [ "$2" = "$3" ]; then echo "  PASS: $1 ($2)"; pass=$((pass+1)); else echo "  FAIL: $1 expected=[$3] got=[$2]"; fail=$((fail+1)); fi; }
check_true() { if [ "$2" = "1" ]; then echo "  PASS: $1"; pass=$((pass+1)); else echo "  FAIL: $1"; fail=$((fail+1)); fi; }
# stock quantity of a SKU in a warehouse (by codes/sku), 0 if no stock_item
stock() {
  local sku="$1" wcode="$2"
  post query "{\"eql\":\"SELECT quantity FROM stock_item WHERE product_id = @p AND warehouse_id = @w\",\"parameters\":[{\"name\":\"p\",\"value\":\"$sku\"},{\"name\":\"w\",\"value\":\"$wcode\"}]}" >/dev/null 2>&1
  # resolve ids first
  local pid wid
  pid=$(post query "{\"eql\":\"SELECT id FROM product WHERE sku = @s\",\"parameters\":[{\"name\":\"s\",\"value\":\"$sku\"}]}" | py 'd["data"]["records"][0]["id"]')
  wid=$(post query "{\"eql\":\"SELECT id FROM warehouse WHERE code = @c\",\"parameters\":[{\"name\":\"c\",\"value\":\"$wcode\"}]}" | py 'd["data"]["records"][0]["id"]')
  post query "{\"eql\":\"SELECT quantity FROM stock_item WHERE product_id = @p AND warehouse_id = @w\",\"parameters\":[{\"name\":\"p\",\"value\":\"$pid\"},{\"name\":\"w\",\"value\":\"$wid\"}]}" \
    | py 'd["data"]["records"][0]["quantity"] if d["data"]["records"] else 0'
}

echo "===== BOOTSTRAP CHECK: v3 entities present ====="
for ent in customer_address product_barcode product_supplier contact sales_agent price_list price_list_item exchange_rate discount_rule bin_location cycle_count cycle_count_line sales_return sales_return_line; do
  ok=$(curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/$ent" "${A[@]}" | py '1 if d.get("data") and d["data"].get("name") else 0')
  check_true "entity $ent exists" "$ok"
done

echo "===== 1. CUSTOMER PROFILE ====="
CP=$(post composed/customer-profile '{"name":"Test Co v3","billing":{"line1":"Via Roma 1","city":"Milan","country":"Italy","postal_code":"20100"},"ship":{"line1":"Corso Italia 2","city":"Turin","country":"Italy","postal_code":"10100"},"contacts":[{"name":"Procurement Desk","role":"buyer","email":"buy@testco.example","phone":"+39 02 1111"}],"email":"info@testco.example","currency":"EUR"}')
echo "$CP"
CPID=$(echo "$CP" | py 'd["data"]["result"]["customer_id"]')
check_true "customer_id present" "$(echo "$CP" | py '1 if d["data"]["result"].get("customer_id") else 0')"
check "addresses count" "$(echo "$CP" | py 'len(d["data"]["result"]["addresses"])')" "2"
check "contacts count" "$(echo "$CP" | py 'len(d["data"]["result"]["contacts"])')" "1"
check "first contact primary" "$(echo "$CP" | py '1 if d["data"]["result"]["contacts"][0]["is_primary"] else 0')" "1"

echo "===== 2. ENRICH PRODUCT (SKU-2001) ====="
EP=$(post composed/enrich-product '{"sku":"SKU-2001","barcodes":[{"code":"9001234567890","barcode_type":"ean13"}],"suppliers":[{"supplier_name":"Global Packaging Co","supplier_sku":"GPC-CUPS","lead_time_days":10,"unit_cost":3.0,"is_default":true}]}')
echo "$EP"
check "barcodes_added" "$(echo "$EP" | py 'd["data"]["result"]["barcodes_added"]')" "1"
check "suppliers_added" "$(echo "$EP" | py 'd["data"]["result"]["suppliers_added"]')" "1"

echo "===== 3. RESOLVE PRICE ====="
RP_DELTA=$(post composed/resolve-price '{"sku":"SKU-1001","customer_name":"Delta Office Supplies","quantity":10}')
echo "$RP_DELTA"
check "Delta discount_percent (VIP 10%)" "$(echo "$RP_DELTA" | py 'int(d["data"]["result"]["discount_percent"])')" "10"
check "Delta currency" "$(echo "$RP_DELTA" | py 'd["data"]["result"]["currency"]')" "USD"
echo "  -> Delta unit_price = $(echo "$RP_DELTA" | py 'd["data"]["result"]["unit_price"]') (base 18.50, -10%, EUR->USD /0.92)"
RP_ALPINE=$(post composed/resolve-price '{"sku":"SKU-1001","customer_name":"Alpine Coffee Roasters","quantity":10}')
echo "$RP_ALPINE"
check "Alpine base_price (wholesale list 16.00)" "$(echo "$RP_ALPINE" | py 'float(d["data"]["result"]["base_price"])==16.0 and 1 or 0')" "1"
check "Alpine currency" "$(echo "$RP_ALPINE" | py 'd["data"]["result"]["currency"]')" "EUR"
echo "  -> Alpine unit_price = $(echo "$RP_ALPINE" | py 'd["data"]["result"]["unit_price"]')"

echo "===== 4. ORDER WITH TERMS (Bavarian Deli) ====="
OWT=$(post composed/order-with-terms '{"customer_name":"Bavarian Deli","lines":[{"sku":"SKU-2001","quantity":5}],"confirmed_date":"2026-09-12","planned_date":"2026-09-20","transport_terms":"FCA","agent_name":"Anna Verdi"}')
echo "$OWT"
OWTID=$(echo "$OWT" | py 'd["data"]["result"]["order_id"]')
check "confirmed_date" "$(echo "$OWT" | py 'd["data"]["result"]["confirmed_date"]')" "2026-09-12"
check "planned_delivery_date" "$(echo "$OWT" | py 'd["data"]["result"]["planned_delivery_date"]')" "2026-09-20"
check "transport_terms" "$(echo "$OWT" | py 'd["data"]["result"]["transport_terms"]')" "FCA"
check "agent" "$(echo "$OWT" | py 'd["data"]["result"]["agent"]')" "Anna Verdi"

echo "===== 5. DELIVER WITH TRANSPORT ====="
DWT=$(post composed/deliver-transport "{\"order_id\":\"$OWTID\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":5}],\"warehouse_code\":\"WH1\",\"transport_cause\":\"vendita\",\"carrier\":\"DHL\",\"tracking_number\":\"TRK123\"}")
echo "$DWT"
DLID=$(echo "$DWT" | py 'd["data"]["result"]["delivery_id"]')
check "transport_cause" "$(echo "$DWT" | py 'd["data"]["result"]["transport_cause"]')" "vendita"
check "carrier" "$(echo "$DWT" | py 'd["data"]["result"]["carrier"]')" "DHL"
check "tracking_number" "$(echo "$DWT" | py 'd["data"]["result"]["tracking_number"]')" "TRK123"
# verify persisted on the goods_delivery record
DLREC=$(post query "{\"eql\":\"SELECT carrier, tracking_number, transport_cause FROM goods_delivery WHERE id = @id\",\"parameters\":[{\"name\":\"id\",\"value\":\"$DLID\"}]}")
check "goods_delivery.carrier persisted" "$(echo "$DLREC" | py 'd["data"]["records"][0]["carrier"]')" "DHL"

echo "===== 6. CONSOLIDATE INVOICE (two orders, same customer) ====="
ALPINE=$(post query '{"eql":"SELECT id FROM customer WHERE name = @n","parameters":[{"name":"n","value":"Alpine Coffee Roasters"}]}' | py 'd["data"]["records"][0]["id"]')
OA=$(post composed/sales-order "{\"customer_id\":\"$ALPINE\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":5}]}")
OAID=$(echo "$OA" | py 'd["data"]["result"]["order_id"]')
OB=$(post composed/sales-order "{\"customer_id\":\"$ALPINE\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":3},{\"sku\":\"SKU-2001\",\"quantity\":2}]}")
OBID=$(echo "$OB" | py 'd["data"]["result"]["order_id"]')
post composed/deliver "{\"order_id\":\"$OAID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":5}],\"warehouse_code\":\"WH1\"}" >/dev/null
post composed/deliver "{\"order_id\":\"$OBID\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":3},{\"sku\":\"SKU-2001\",\"quantity\":2}],\"warehouse_code\":\"WH1\"}" >/dev/null
CI=$(post composed/consolidate-invoice "{\"order_ids\":[\"$OAID\",\"$OBID\"],\"due_days\":14}")
echo "$CI"
check "order_count" "$(echo "$CI" | py 'd["data"]["result"]["order_count"]')" "2"
check_true "invoice grand_total > 0" "$(echo "$CI" | py '1 if float(d["data"]["result"]["grand_total"])>0 else 0')"

echo "===== 7. CYCLE COUNT (SKU-1001 in WH1, counted = current + 7) ====="
CUR=$(stock SKU-1001 WH1)
COUNTED=$(python3 -c "print(round($CUR + 7, 2))")
echo "  current stock SKU-1001 WH1 = $CUR ; counted = $COUNTED"
CC=$(post composed/cycle-count "{\"warehouse_code\":\"WH1\",\"lines\":[{\"sku\":\"SKU-1001\",\"counted_qty\":$COUNTED}]}")
echo "$CC"
check "variance" "$(echo "$CC" | py 'int(round(float(d["data"]["result"]["lines"][0]["variance"])))')" "7"
AFTER=$(stock SKU-1001 WH1)
DIFF=$(python3 -c "print(int(round($AFTER - $CUR)))")
check "stock increased by 7" "$DIFF" "7"

echo "===== 8. CUSTOMER RETURN (restock against the delivered order) ====="
BEFORE=$(stock SKU-2001 WH1)
RET=$(post composed/customer-return "{\"delivery_id\":\"$DLID\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":4,\"disposition\":\"restock\"}],\"warehouse_code\":\"WH1\"}")
echo "$RET"
check_true "return_id present" "$(echo "$RET" | py '1 if d["data"]["result"].get("return_id") else 0')"
check "restocked count" "$(echo "$RET" | py 'len(d["data"]["result"]["restocked"])')" "1"
AFTER2=$(stock SKU-2001 WH1)
DIFF2=$(python3 -c "print(int(round($AFTER2 - $BEFORE)))")
check "restock increased stock by 4" "$DIFF2" "4"

echo "======================================"
echo "RESULT: PASS=$pass FAIL=$fail"
[ "$fail" -eq 0 ] && echo "ALL GREEN" || echo "SOME FAILURES"
