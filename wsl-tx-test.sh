#!/usr/bin/env bash
# Definitive test of the transaction wrapper on Composed().
# Positive create persists; a create that fails on the 2nd line must roll back
# the header + first line (count unchanged).
set +e
BASE=http://127.0.0.1:5080
CUST=d537c7f3-5d22-d75e-bfb3-92f239889580
SKU=SKU-1001
BADSKU=ZZZ-DOES-NOT-EXIST
TOKEN=$(curl -s -X POST $BASE/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')

cnt() { curl -s -X POST $BASE/api/v3.0/p/agent/query -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "{\"eql\":\"SELECT * FROM $1\"}" | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["total_count"])'; }
succ() { python3 -c 'import sys,json; print(json.load(sys.stdin)["success"])'; }

BQ=$(cnt quote); BL=$(cnt quote_line)
echo "baseline: quotes=$BQ quote_lines=$BL"

echo "--- POSITIVE: create quote with 1 valid line ---"
POS=$(curl -s -X POST $BASE/api/v3.0/p/agent/composed/quote -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"customer_id\":\"$CUST\",\"lines\":[{\"sku\":\"$SKU\",\"quantity\":2}]}")
echo "positive success: $(echo "$POS" | succ)"
QAP=$(cnt quote); LAP=$(cnt quote_line)
echo "after positive: quotes=$QAP quote_lines=$LAP"

echo "--- NEGATIVE: create quote with [valid, BAD sku] (fails on 2nd line) ---"
NEG=$(curl -s -X POST $BASE/api/v3.0/p/agent/composed/quote -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"customer_id\":\"$CUST\",\"lines\":[{\"sku\":\"$SKU\",\"quantity\":1},{\"sku\":\"$BADSKU\",\"quantity\":1}]}")
echo "negative success: $(echo "$NEG" | succ)"
echo "negative error: $(echo "$NEG" | python3 -c 'import sys,json; print(json.load(sys.stdin).get("error"))')"
QN=$(cnt quote); LN=$(cnt quote_line)
echo "after negative: quotes=$QN quote_lines=$LN"

echo "================ RESULT ================"
if [ "$QAP" == "$((BQ+1))" ]; then echo "PASS: positive create persisted (+1 quote)"; else echo "FAIL: positive create did not persist (expected $((BQ+1)), got $QAP)"; fi
if [ "$QN" == "$QAP" ]; then echo "PASS: failed create rolled back (quote count unchanged)"; else echo "FAIL: failed create left an orphan (quote went $QAP -> $QN)"; fi
if [ "$LN" == "$LAP" ]; then echo "PASS: failed create rolled back the first line too (quote_line unchanged)"; else echo "FAIL: failed create left an orphan line (quote_line went $LAP -> $LN)"; fi
