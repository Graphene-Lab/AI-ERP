#!/usr/bin/env bash
# Build Site with the new setup-status/reprovision endpoints, restart ERP (NO db drop),
# then probe both endpoints directly.
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site || exit 1
echo "=== build Site ==="
dotnet build -c Release -v q -clp:ErrorsOnly
echo "SITE_BUILD_EXIT=$?"

echo "=== restart ERP (no db drop) ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 3
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp.log 2>&1 &
echo "started pid $!"
sleep 22

echo "=== JWT ==="
TOKEN=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
echo "token len: ${#TOKEN}"

echo "=== setup-status ==="
curl -s -X GET http://127.0.0.1:5080/api/v3.0/p/agent/setup-status -H "Authorization: Bearer $TOKEN"
echo ""
echo "=== reprovision (expect already applied) ==="
curl -s -X POST http://127.0.0.1:5080/api/v3.0/p/agent/reprovision -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{}'
echo ""
