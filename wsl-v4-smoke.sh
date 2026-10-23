#!/usr/bin/env bash
# v4 daily-use composed-operations smoke test (deterministic, no LLM).
# Exercises each new composed op against a freshly-bootstrapped v4 database.
# Idempotent: a unique run TAG keeps created names from colliding across re-runs.
BASE=${BASE:-http://127.0.0.1:5080}
TAG=$(date +%s)
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json')
post()  { curl -s -m 25 -X POST  "$BASE/api/v3.0/p/agent/$1" "${A[@]}" -d "$2"; }
patch() { curl -s -m 25 -X PATCH "$BASE/api/v3.0/p/agent/$1" "${A[@]}" -d "$2"; }
py() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

pass=0; fail=0
check() { if [ "$2" = "$3" ]; then echo "  PASS: $1 ($2)"; pass=$((pass+1)); else echo "  FAIL: $1 expected=[$3] got=[$2]"; fail=$((fail+1)); fi; }
check_true() { if [ "$2" = "1" ]; then echo "  PASS: $1"; pass=$((pass+1)); else echo "  FAIL: $1"; fail=$((fail+1)); fi; }
# stock quantity of a SKU in a warehouse (by sku/warehouse code), 0 if no stock_item
stock() {
  local sku="$1" wcode="$2" pid wid
  pid=$(post query "{\"eql\":\"SELECT id FROM product WHERE sku = @s\",\"parameters\":[{\"name\":\"s\",\"value\":\"$sku\"}]}" | py 'd["data"]["records"][0]["id"]')
  wid=$(post query "{\"eql\":\"SELECT id FROM warehouse WHERE code = @c\",\"parameters\":[{\"name\":\"c\",\"value\":\"$wcode\"}]}" | py 'd["data"]["records"][0]["id"]')
  post query "{\"eql\":\"SELECT quantity FROM stock_item WHERE product_id = @p AND warehouse_id = @w\",\"parameters\":[{\"name\":\"p\",\"value\":\"$pid\"},{\"name\":\"w\",\"value\":\"$wid\"}]}" \
    | py 'float(d["data"]["records"][0]["quantity"]) if d["data"]["records"] else 0'
}
# count rows of an entity matching a guid field
count() {
  local ent="$1" field="$2" val="$3"
  post query "{\"eql\":\"SELECT id FROM $ent WHERE $field = @v\",\"parameters\":[{\"name\":\"v\",\"value\":\"$val\"}]}" | py 'len(d["data"]["records"])'
}

echo "===== BOOTSTRAP CHECK: v4 entities present ====="
for ent in invoice_installment debit_note attachment document_audit bank_transaction reservation supplier_return supplier_return_line; do
  ok=$(curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/$ent" "${A[@]}" | py '1 if d.get("data") and d["data"].get("name") else 0')
  check_true "entity $ent exists" "$ok"
done
# invoice has the new v4 fields
INV_FIELDS=$(curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/invoice" "${A[@]}" | py '",".join(f["name"] for f in d["data"]["fields"])')
for fld in document_discount_percent stamp_tax withholding_percent withholding_amount reverse_charge tax_document_type customs_amount posted posted_date storned storno_of customer_visible_notes; do
  check_true "invoice.$fld present" "$(echo "$INV_FIELDS" | grep -q "$fld" && echo 1 || echo 0)"
done
# invoice_line.line_type and payment method options
IL_LT=$(curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/invoice_line" "${A[@]}" | py '1 if any(f["name"]=="line_type" for f in d["data"]["fields"]) else 0')
check_true "invoice_line.line_type present" "$IL_LT"
# bank_transaction seed rows (count all; bool fields can't be matched as text in EQL)
BTC=$(post query '{"eql":"SELECT id FROM bank_transaction"}' | py 'len(d["data"]["records"])')
check_true "bank_transaction seeded (>=2)" "$(python3 -c "print(1 if $BTC>=2 else 0)")"

# Fresh customer for this run (avoids unique-name collisions on re-runs)
CUST="SmokeCust-$TAG"
post records/customer "{\"name\":\"$CUST\",\"currency\":\"EUR\",\"blocked\":false}" >/dev/null

echo "===== 1. SALES INVOICE FROM SCRATCH ====="
SI=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":2,\"unit_price\":18.50,\"vatCode\":\"V10\"},{\"sku\":\"SKU-2001\",\"quantity\":5,\"unit_price\":6.90,\"vatCode\":\"V22\"}],\"document_discount\":5,\"accessory_lines\":[{\"description\":\"Freight\",\"amount\":12.00,\"vatCode\":\"V22\",\"type\":\"transport\"}],\"stamp_tax\":2.00,\"withholding_percent\":5,\"reverse_charge\":false,\"tax_document_type\":\"taxable\",\"currency\":\"EUR\",\"installments\":[{\"due_date\":\"2026-10-15\",\"amount\":46.22},{\"due_date\":\"2026-11-15\",\"amount\":46.23}]}")
echo "$SI"
SIID=$(echo "$SI" | py 'd["data"]["result"]["invoice_id"]')
check_true "invoice_id present" "$(echo "$SI" | py '1 if d["data"]["result"].get("invoice_id") else 0')"
# document_discount = 5% of product subtotal (71.50) = 3.58
check "document_discount" "$(echo "$SI" | py 'float(d["data"]["result"]["document_discount"])==3.58 and 1 or 0')" "1"
# grand_total = (subtotal - doc) + transport(12) + vat + stamp - withholding
GT=$(echo "$SI" | py 'float(d["data"]["result"]["grand_total"])')
EXP=$(echo "$SI" | py 'round((float(d["data"]["result"]["subtotal"]) - float(d["data"]["result"]["document_discount"]) + 12.00 + float(d["data"]["result"]["vat_total"]) + float(d["data"]["result"]["stamp_tax"]) - float(d["data"]["result"]["withholding_amount"])),2)')
check "grand_total consistency" "$(python3 -c "print(1 if abs($GT-$EXP)<0.01 else 0)")" "1"
check "installments returned (2)" "$(echo "$SI" | py 'len(d["data"]["result"]["installments"])')" "2"
check "installment rows persisted (2)" "$(count invoice_installment invoice_id "$SIID")" "2"
check "invoice_line rows persisted (3)" "$(count invoice_line invoice_id "$SIID")" "3"
TRTYPE=$(post query "{\"eql\":\"SELECT line_type FROM invoice_line WHERE invoice_id = @id AND line_type = @t\",\"parameters\":[{\"name\":\"id\",\"value\":\"$SIID\"},{\"name\":\"t\",\"value\":\"transport\"}]}" | py 'len(d["data"]["records"])')
check "transport line persisted" "$TRTYPE" "1"

echo "===== 2. PURCHASE INVOICE FROM SCRATCH ====="
PI=$(post composed/purchase-invoice-scratch "{\"supplier_name\":\"Prime Coffee Importers\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":10,\"unit_cost\":9.0},{\"sku\":\"SKU-1002\",\"quantity\":20,\"unit_cost\":5.5}],\"currency\":\"EUR\"}")
echo "$PI"
check_true "bill_id present" "$(echo "$PI" | py '1 if d["data"]["result"].get("bill_id") else 0')"
check_true "grand_total > 0" "$(echo "$PI" | py '1 if float(d["data"]["result"]["grand_total"])>0 else 0')"

echo "===== 3. DEBIT NOTE ====="
DN=$(post composed/debit-note "{\"supplier_name\":\"Global Packaging Co\",\"amount\":250.00,\"reason\":\"extra charge\"}")
echo "$DN"
check_true "debit_note_number present" "$(echo "$DN" | py '1 if d["data"]["result"].get("debit_note_number") else 0')"

echo "===== 4. PROFORMA -> INVOICE ====="
PF=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":1,\"unit_price\":6.90,\"vatCode\":\"V22\"}],\"currency\":\"EUR\"}")
PFID=$(echo "$PF" | py 'd["data"]["result"]["invoice_id"]')
patch records/invoice/$PFID '{"invoice_type":"proforma"}' >/dev/null
P2I=$(post composed/proforma-to-invoice "{\"proforma_invoice_id\":\"$PFID\"}")
echo "$P2I"
check "invoice_type now immediate" "$(echo "$P2I" | py 'd["data"]["result"]["invoice_type"]')" "immediate"

echo "===== 5. POST INVOICE (and double-post fails) ====="
PINV=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":1,\"unit_price\":6.90,\"vatCode\":\"V22\"}],\"currency\":\"EUR\"}")
PINVID=$(echo "$PINV" | py 'd["data"]["result"]["invoice_id"]')
P1=$(post composed/post-invoice "{\"invoice_id\":\"$PINVID\"}")
echo "$P1"
check "posted true" "$(echo "$P1" | py '1 if d["data"]["result"]["posted"] else 0')" "1"
P2=$(post composed/post-invoice "{\"invoice_id\":\"$PINVID\"}")
check "second post fails (success=false)" "$(echo "$P2" | py '0 if d.get("success") else 1')" "1"

echo "===== 6. STORNO INVOICE ====="
ST=$(post composed/storno-invoice "{\"invoice_id\":\"$PINVID\"}")
echo "$ST"
STID=$(echo "$ST" | py 'd["data"]["result"]["storno_invoice_id"]')
STGT=$(post query "{\"eql\":\"SELECT grand_total FROM invoice WHERE id = @id\",\"parameters\":[{\"name\":\"id\",\"value\":\"$STID\"}]}" | py 'float(d["data"]["records"][0]["grand_total"])')
check_true "storno grand_total < 0" "$(python3 -c "print(1 if $STGT<0 else 0)")"
ORIGST=$(post query "{\"eql\":\"SELECT storned FROM invoice WHERE id = @id\",\"parameters\":[{\"name\":\"id\",\"value\":\"$PINVID\"}]}" | py '1 if d["data"]["records"][0]["storned"] else 0')
check "original storned=true" "$ORIGST" "1"

echo "===== 7. DUPLICATE INVOICE ====="
DUP=$(post composed/duplicate-invoice "{\"invoice_id\":\"$PINVID\"}")
echo "$DUP"
DUPID=$(echo "$DUP" | py 'd["data"]["result"]["new_invoice_id"]')
ORIGNUM=$(post query "{\"eql\":\"SELECT invoice_number FROM invoice WHERE id = @id\",\"parameters\":[{\"name\":\"id\",\"value\":\"$PINVID\"}]}" | py 'd["data"]["records"][0]["invoice_number"]')
DUPNUM=$(echo "$DUP" | py 'd["data"]["result"]["new_invoice_number"]')
check_true "new number differs" "$(python3 -c "print(1 if '$ORIGNUM' != '$DUPNUM' else 0)")"
DUPST=$(post query "{\"eql\":\"SELECT status FROM invoice WHERE id = @id\",\"parameters\":[{\"name\":\"id\",\"value\":\"$DUPID\"}]}" | py 'd["data"]["records"][0]["status"]')
check "duplicate status draft" "$DUPST" "draft"

echo "===== 8. CANCEL INVOICE ====="
CINV=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":1,\"unit_price\":6.90,\"vatCode\":\"V22\"}],\"currency\":\"EUR\"}")
CINVID=$(echo "$CINV" | py 'd["data"]["result"]["invoice_id"]')
CX=$(post composed/cancel-invoice "{\"invoice_id\":\"$CINVID\"}")
echo "$CX"
check "status cancelled" "$(echo "$CX" | py 'd["data"]["result"]["status"]')" "cancelled"

echo "===== 9. RECORD COLLECTION (2 invoices + allowance) ====="
CA=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":10,\"unit_price\":6.90,\"vatCode\":\"V22\"}],\"currency\":\"EUR\"}")
CB=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":10,\"unit_price\":6.90,\"vatCode\":\"V22\"}],\"currency\":\"EUR\"}")
CAID=$(echo "$CA" | py 'd["data"]["result"]["invoice_id"]')
CBID=$(echo "$CB" | py 'd["data"]["result"]["invoice_id"]')
gCA=$(echo "$CA" | py 'float(d["data"]["result"]["grand_total"])')
gCB=$(echo "$CB" | py 'float(d["data"]["result"]["grand_total"])')
RC=$(post composed/record-collection "{\"invoice_ids\":[\"$CAID\",\"$CBID\"],\"amount\":100.00,\"method\":\"bank\",\"allowance\":10.00}")
echo "$RC"
COL=$(echo "$RC" | py 'float(d["data"]["result"]["collected"])')
ALLW=$(echo "$RC" | py 'float(d["data"]["result"]["allowance"])')
check "collected == 100" "$(python3 -c "print(1 if abs($COL-100.0)<0.01 else 0)")" "1"
check "allowance == 10" "$(python3 -c "print(1 if abs($ALLW-10.0)<0.01 else 0)")" "1"
SUMBAL=$(echo "$RC" | py 'round(sum(float(i["balance"]) for i in d["data"]["result"]["invoices"]),2)')
EXPBAL=$(python3 -c "print(round($gCA+$gCB-100.0-10.0,2))")
check "balances reduced by collected+allowance" "$(python3 -c "print(1 if abs($SUMBAL-$EXPBAL)<0.01 else 0)")" "1"
check "first invoice paid" "$(echo "$RC" | py 'd["data"]["result"]["invoices"][0]["status"]')" "paid"
check "second invoice partial" "$(echo "$RC" | py 'd["data"]["result"]["invoices"][1]["status"]')" "partial"

echo "===== 10. CUSTOMER STATEMENT ====="
CS=$(post composed/customer-statement "{\"customer_name\":\"$CUST\"}")
echo "$CS"
check_true "total_open >= 0" "$(echo "$CS" | py '1 if float(d["data"]["result"]["total_open"])>=0 else 0')"
check_true "open_items is a list" "$(echo "$CS" | py '1 if isinstance(d["data"]["result"]["open_items"],list) else 0')"
BUCKETS_OK=$(echo "$CS" | py '1 if all(i.get("bucket") in ("current","1-30","31-60","61-90","90+") for i in d["data"]["result"]["open_items"]) else 0')
check "all items have a valid bucket" "$BUCKETS_OK" "1"

echo "===== 11. RESERVE / RELEASE STOCK ====="
RES=$(post composed/reserve-stock '{"sku":"SKU-1001","warehouse_code":"WH1","quantity":10}')
echo "$RES"
check "reserved == 10" "$(echo "$RES" | py 'float(d["data"]["result"]["reserved"])==10.0 and 1 or 0')" "1"
PID=$(post query '{"eql":"SELECT id FROM product WHERE sku = @s","parameters":[{"name":"s","value":"SKU-1001"}]}' | py 'd["data"]["records"][0]["id"]')
WID=$(post query '{"eql":"SELECT id FROM warehouse WHERE code = @c","parameters":[{"name":"c","value":"WH1"}]}' | py 'd["data"]["records"][0]["id"]')
RESC=$(post query "{\"eql\":\"SELECT id FROM reservation WHERE product_id = @p AND warehouse_id = @w\",\"parameters\":[{\"name\":\"p\",\"value\":\"$PID\"},{\"name\":\"w\",\"value\":\"$WID\"}]}" | py 'len(d["data"]["records"])')
check "reservation row created" "$RESC" "1"
REL=$(post composed/release-stock '{"sku":"SKU-1001","warehouse_code":"WH1","quantity":10}')
echo "$REL"
check "released == 10" "$(echo "$REL" | py 'float(d["data"]["result"]["released"])==10.0 and 1 or 0')" "1"
RESC2=$(post query "{\"eql\":\"SELECT id FROM reservation WHERE product_id = @p AND warehouse_id = @w\",\"parameters\":[{\"name\":\"p\",\"value\":\"$PID\"},{\"name\":\"w\",\"value\":\"$WID\"}]}" | py 'len(d["data"]["records"])')
check "reservation rows removed" "$RESC2" "0"

echo "===== 12. SUPPLIER RETURN (stock decreased by 3) ====="
BEFORE=$(stock SKU-1001 WH1)
SR=$(post composed/supplier-return '{"supplier_name":"Prime Coffee Importers","lines":[{"sku":"SKU-1001","quantity":3,"reason":"damaged"}],"warehouse_code":"WH1"}')
echo "$SR"
AFTER=$(stock SKU-1001 WH1)
DIFF=$(python3 -c "print(round($BEFORE - $AFTER,2))")
check "stock decreased by 3" "$DIFF" "3.0"

echo "======================================"
echo "RESULT: PASS=$pass FAIL=$fail"
[ "$fail" -eq 0 ] && echo "ALL GREEN" || echo "SOME FAILURES"
