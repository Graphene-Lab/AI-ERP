#!/usr/bin/env bash
# Probe: record-collection by invoice_numbers (new path).
BASE=${BASE:-http://127.0.0.1:5080}
TAG=$(date +%s)
TOKEN=$(curl -s -m 10 -X POST "$BASE/api/v3/en_US/auth/jwt/token" -H 'Content-Type: application/json' \
  -d '{"email":"erp@webvella.com","password":"erp"}' | python3 -c 'import sys,json; print(json.load(sys.stdin)["object"])')
A=(-H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json')
post() { curl -s -m 25 -X POST "$BASE/api/v3.0/p/agent/$1" "${A[@]}" -d "$2"; }
py() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

CUST="ProbeColl-$TAG"
post records/customer "{\"name\":\"$CUST\",\"currency\":\"EUR\"}" >/dev/null
I1=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-1001\",\"quantity\":1,\"unit_price\":100.00,\"vatCode\":\"V22\"}],\"currency\":\"EUR\"}")
I2=$(post composed/sales-invoice "{\"customer_name\":\"$CUST\",\"lines\":[{\"sku\":\"SKU-2001\",\"quantity\":1,\"unit_price\":50.00,\"vatCode\":\"V10\"}],\"currency\":\"EUR\"}")
N1=$(echo "$I1" | py 'd["data"]["result"]["invoice_number"]')
N2=$(echo "$I2" | py 'd["data"]["result"]["invoice_number"]')
G1=$(echo "$I1" | py 'float(d["data"]["result"]["grand_total"])')
G2=$(echo "$I2" | py 'float(d["data"]["result"]["grand_total"])')
echo "inv1 $N1 grand=$G1 ; inv2 $N2 grand=$G2"
RC=$(post composed/record-collection "{\"invoice_numbers\":[\"$N1\",\"$N2\"],\"amount\":175.00,\"method\":\"bank\",\"allowance\":2.00}")
echo "$RC"
echo "collected=$(echo "$RC" | py 'float(d["data"]["result"]["collected"])') allowance=$(echo "$RC" | py 'float(d["data"]["result"]["allowance"])')"
echo "statuses=$(echo "$RC" | py '[i["status"] for i in d["data"]["result"]["invoices"]]')"
