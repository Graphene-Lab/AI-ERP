#!/usr/bin/env bash
# Quick read-only check that the v4 schema is deployed on the live ERP.
BASE=${BASE:-http://127.0.0.1:5080}
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN")
py() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

echo "v4 entities:"
for ent in invoice_installment debit_note attachment document_audit bank_transaction reservation supplier_return supplier_return_line; do
  ok=$(curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/$ent" "${A[@]}" | py '1 if d.get("data") and d["data"].get("name") else 0')
  echo "  $ent -> $([ "$ok" = "1" ] && echo OK || echo MISSING)"
done
echo "invoice new fields:"
INV=$(curl -s -m 15 "$BASE/api/v3.0/p/agent/schema/invoice" "${A[@]}" | py '",".join(f["name"] for f in d["data"]["fields"])')
for fld in document_discount_percent stamp_tax withholding_percent withholding_amount reverse_charge tax_document_type customs_amount posted posted_date storned storno_of; do
  echo "  invoice.$fld -> $(echo "$INV" | grep -q "$fld" && echo OK || echo MISSING)"
done
echo "seeded bank_transaction rows: $(curl -s -m 15 -X POST "$BASE/api/v3.0/p/agent/query" "${A[@]}" -H 'Content-Type: application/json' -d '{"eql":"SELECT id FROM bank_transaction"}' | py 'len(d["data"]["records"])')"
