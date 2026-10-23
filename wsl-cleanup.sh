#!/usr/bin/env bash
# Clean up wizard test artifacts and restore the dev DB to the original seed.
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP || exit 1
export PGPASSWORD=dev

echo "=== remove test artifacts ==="
rm -f AI.Erp.Site/bootstrap.json.bak
rm -rf /tmp/wizard-test /tmp/wizard-fresh
echo "removed .bak and /tmp wizard dirs"

echo "=== re-seed dev DB with the original bootstrap.json (Northwind) ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 2
sudo -u postgres psql -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='erp' AND pid <> pg_backend_pid();" >/dev/null 2>&1
sudo -u postgres psql -c "DROP DATABASE IF EXISTS erp;" >/dev/null 2>&1
sudo -u postgres createdb -O dev erp 2>&1
cd AI.Erp.Site
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp.log 2>&1 &
echo "restarted pid $!, waiting..."
sleep 26
token=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
echo "company rows now: $(psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT name FROM rec_company;")"
echo "setup-status installed: $(curl -s http://127.0.0.1:5080/api/v3.0/p/agent/setup-status -H "Authorization: Bearer $token" | grep -o '"installed":[a-z]*')"
