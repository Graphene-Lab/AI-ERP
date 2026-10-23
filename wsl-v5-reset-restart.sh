#!/usr/bin/env bash
# Reset DB to fresh seed + restart ERP (Site already built with v5). No rebuild.
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP || exit 1

echo "=== stop ERP ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 3
ps aux | grep "AI.Erp.Site.dll" | grep -v grep || echo "ERP stopped"

echo "=== drop + recreate erp db ==="
sudo -u postgres psql -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='erp' AND pid <> pg_backend_pid();" >/dev/null 2>&1
sudo -u postgres psql -c "DROP DATABASE IF EXISTS erp;" 2>&1
sudo -u postgres createdb -O dev erp 2>&1
echo -n "tables after reset: "
PGPASSWORD=dev psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"

echo "=== start ERP ==="
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site || exit 1
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp.log 2>&1 &
echo "started pid $!"
echo "waiting for bootstrap (seed) ..."
sleep 25
echo "=== verify JWT ==="
curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | head -c 80
echo ""
echo "=== aging baseline (should be all zero) ==="
TOKEN=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
curl -s -X POST http://127.0.0.1:5080/api/v3.0/p/agent/composed/aging-receivables -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{}' | head -c 260
echo ""
