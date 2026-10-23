#!/usr/bin/env bash
# Build Site with the transaction-wrapped Composed(), restart ERP (no db drop),
# probe the query endpoint to learn the response shape.
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site || exit 1

echo "=== build Site (Release) ==="
dotnet build -c Release -v q -clp:ErrorsOnly
echo "SITE_BUILD_EXIT=$?"

echo "=== restart ERP (no db drop) ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 3
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp.log 2>&1 &
echo "started pid $!"
sleep 22

BASE=http://127.0.0.1:5080
TOKEN=$(curl -s -X POST $BASE/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
echo "token len: ${#TOKEN}"

echo "=== query: customers (raw) ==="
curl -s -X POST $BASE/api/v3.0/p/agent/query -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"eql":"SELECT TOP 2 id, name FROM customer"}'
echo ""
echo "=== query: products (raw) ==="
curl -s -X POST $BASE/api/v3.0/p/agent/query -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"eql":"SELECT TOP 2 sku FROM product"}'
echo ""
echo "=== query: quote count (raw) ==="
curl -s -X POST $BASE/api/v3.0/p/agent/query -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"eql":"SELECT TOP 1 id FROM quote"}'
echo ""
