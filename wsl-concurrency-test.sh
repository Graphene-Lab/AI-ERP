#!/usr/bin/env bash
# Concurrency test for the stock advisory lock (#2).
# adjust-stock computes delta = target - current, then AdjustStock applies it.
# Firing N concurrent "set to 100" on a product at 0 is a lost-update probe:
#   without the lock  -> each reads 0, adds 100 -> final up to N*100 (over-count)
#   with the lock     -> serialized, all converge -> final exactly 100
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site || exit 1

export PGPASSWORD=dev
PSQL="psql -h 127.0.0.1 -U dev -d erp -tAc"

echo "=== restart ERP (no db drop) ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 3
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp.log 2>&1 &
echo "started pid $!"
sleep 22

TOKEN=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
echo "token len: ${#TOKEN}"
if [ ${#TOKEN} -lt 20 ]; then echo "ERP not up, tail log:"; tail -20 /tmp/erp.log; exit 1; fi

SKU=$($PSQL "SELECT sku FROM rec_product WHERE sku IS NOT NULL AND sku <> '' LIMIT 1;")
WH=$($PSQL "SELECT code FROM rec_warehouse LIMIT 1;")
echo "using SKU=$SKU  WH=$WH"
if [ -z "$SKU" ] || [ -z "$WH" ]; then echo "no seed product/warehouse"; exit 1; fi

API=http://127.0.0.1:5080/api/v3.0/p/agent/composed/adjust-stock
H1="Authorization: Bearer $TOKEN"
H2='Content-Type: application/json'

echo "=== baseline: set stock to 0 ==="
curl -s -X POST "$API" -H "$H1" -H "$H2" -d "{\"sku\":\"$SKU\",\"warehouse_code\":\"$WH\",\"new_quantity\":0,\"reason\":\"baseline\"}"
echo ""

read PQ SQ <<< "$($PSQL "SELECT coalesce((SELECT stock_quantity FROM rec_product WHERE sku='$SKU'),0), coalesce((SELECT quantity FROM rec_stock_item si JOIN rec_product p ON si.product_id=p.id WHERE p.sku='$SKU' AND si.warehouse_id=(SELECT id FROM rec_warehouse WHERE code='$WH')),0);")"
echo "after baseline: product.stock_quantity=$PQ  stock_item.quantity=$SQ"

echo "=== fire 12 concurrent adjust-stock to 100 ==="
for i in $(seq 1 12); do
  curl -s -X POST "$API" -H "$H1" -H "$H2" -d "{\"sku\":\"$SKU\",\"warehouse_code\":\"$WH\",\"new_quantity\":100,\"reason\":\"race-$i\"}" > /tmp/race_$i.json &
done
wait
echo "all 12 returned"

read PQ2 SQ2 <<< "$($PSQL "SELECT coalesce((SELECT stock_quantity FROM rec_product WHERE sku='$SKU'),0), coalesce((SELECT quantity FROM rec_stock_item si JOIN rec_product p ON si.product_id=p.id WHERE p.sku='$SKU' AND si.warehouse_id=(SELECT id FROM rec_warehouse WHERE code='$WH')),0);")"
echo "FINAL: product.stock_quantity=$PQ2  stock_item.quantity=$SQ2"
echo "EXPECTED with lock: 100 / 100"
if [ "$PQ2" = "100.00" ] || [ "$PQ2" = "100" ]; then
  echo "RESULT: PASS (converged to 100)"
else
  echo "RESULT: FAIL (over-count -> lost-update not prevented)"
fi

echo "=== sample responses ==="
for i in 1 2 3; do echo "race_$i: $(cat /tmp/race_$i.json 2>/dev/null | head -c 200)"; done
