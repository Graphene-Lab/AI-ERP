#!/usr/bin/env bash
set +e
echo "=== stopping ERP dotnet processes ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 3
ps aux | grep "AI.Erp.Site.dll" | grep -v grep || echo "ERP stopped"

echo "=== dropping and recreating erp database ==="
# Terminate lingering connections, then drop/create.
sudo -u postgres psql -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='erp' AND pid <> pg_backend_pid();" >/dev/null 2>&1
sudo -u postgres psql -c "DROP DATABASE IF EXISTS erp;" 2>&1
sudo -u postgres createdb -O dev erp 2>&1
echo "=== verify empty ==="
PGPASSWORD=dev psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"
