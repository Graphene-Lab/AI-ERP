#!/usr/bin/env bash
# v5: reset DB to fresh seed, rebuild Site (Release) with the new report ops, restart ERP.
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP || exit 1

echo "=== [1/4] stop ERP ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 3
ps aux | grep "AI.Erp.Site.dll" | grep -v grep || echo "ERP stopped"

echo "=== [2/4] drop + recreate erp db ==="
sudo -u postgres psql -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='erp' AND pid <> pg_backend_pid();" >/dev/null 2>&1
sudo -u postgres psql -c "DROP DATABASE IF EXISTS erp;" 2>&1
sudo -u postgres createdb -O dev erp 2>&1
echo -n "tables after reset: "
PGPASSWORD=dev psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"

echo "=== [3/4] build Site (Release) ==="
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site || exit 1
dotnet build -c Release -v q -clp:ErrorsOnly 2>&1 | tail -30
echo "BUILD_EXIT=${PIPESTATUS[0]}"

echo "=== [4/4] start ERP ==="
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp.log 2>&1 &
echo "started pid $!"
echo "waiting for bootstrap (seed) ..."
sleep 25
echo "=== verify JWT ==="
curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | head -c 120
echo ""
echo "=== v5 report endpoint present? ==="
TOKEN=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
curl -s -X POST http://127.0.0.1:5080/api/v3.0/p/agent/composed/aging-receivables -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{}' | head -c 300
echo ""
