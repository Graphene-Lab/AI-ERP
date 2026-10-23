#!/usr/bin/env bash
# Fast re-run: ERP assumed already up. Baseline 0, fire N concurrent set-to-100, read result.
export PGPASSWORD=dev
PSQL="psql -h 127.0.0.1 -U dev -d erp -tAc"
SKU=SKU-2002
WH=WH1
API=http://127.0.0.1:5080/api/v3.0/p/agent/composed/adjust-stock

TOKEN=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
H1="Authorization: Bearer $TOKEN"
H2='Content-Type: application/json'

stock() { $PSQL "SELECT coalesce((SELECT stock_quantity FROM rec_product WHERE sku='$SKU'),-1) || '/' || coalesce((SELECT quantity FROM rec_stock_item si JOIN rec_product p ON si.product_id=p.id WHERE p.sku='$SKU' AND si.warehouse_id=(SELECT id FROM rec_warehouse WHERE code='$WH')),-1);"; }

echo "baseline -> 0"
curl -s -X POST "$API" -H "$H1" -H "$H2" -d "{\"sku\":\"$SKU\",\"warehouse_code\":\"$WH\",\"new_quantity\":0,\"reason\":\"baseline\"}" >/dev/null
echo "stock after baseline: $(stock)"

N=12
echo "firing $N concurrent set-to-100"
for i in $(seq 1 $N); do
  curl -s -X POST "$API" -H "$H1" -H "$H2" -d "{\"sku\":\"$SKU\",\"warehouse_code\":\"$WH\",\"new_quantity\":100,\"reason\":\"race-$i\"}" > /tmp/race_$i.json &
done
wait
echo "all $N returned"
FINAL=$(stock)
echo "FINAL stock (product/stock_item): $FINAL"
if [ "$FINAL" = "100.00/100.00" ] || [ "$FINAL" = "100/100" ] || [ "$FINAL" = "100.0/100.0" ]; then
  echo "RESULT: PASS - converged to 100 (no lost update / over-count)"
else
  echo "RESULT: CHECK - final=$FINAL (expected 100/100)"
fi
echo "success count: $(grep -l '"success":true' /tmp/race_*.json 2>/dev/null | wc -l) / $N"
