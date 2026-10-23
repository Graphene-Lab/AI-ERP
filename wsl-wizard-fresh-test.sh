#!/usr/bin/env bash
# Fresh-install test of the setup wizard: clean DB -> wizard -> start ERP -> verify.
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP || exit 1
export PGPASSWORD=dev

echo "=== [1] drop + recreate erp db (fresh) ==="
sudo -u postgres psql -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='erp' AND pid <> pg_backend_pid();" >/dev/null 2>&1
sudo -u postgres psql -c "DROP DATABASE IF EXISTS erp;" 2>&1
sudo -u postgres createdb -O dev erp 2>&1
echo "tables now: $(psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';")"

echo "=== [2] run the wizard (Italian, non-interactive) ==="
WIZARD_NONINTERACTIVE=1 WIZARD_LANG=it \
COMPANY_NAME="Alpha Test Spa" COMPANY_LEGAL="Alpha Test S.p.A." COMPANY_VAT="IT12345678901" \
COMPANY_ADDRESS="Corso Italia 5" COMPANY_CITY="Torino" COMPANY_COUNTRY="Italy" COMPANY_CURRENCY="EUR" \
DB_HOST=127.0.0.1 DB_PORT=5432 DB_USER=dev DB_PASS=dev DB_NAME=erp \
ERP_USER=erp@webvella.com ERP_PASS=erp \
AB_DIR=/tmp/wizard-fresh ERP_BASE_URL=http://127.0.0.1:5080 \
bash tools/setup-wizard.sh

echo "=== [3] start ERP (first start applies bootstrap.json) ==="
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 2
cd AI.Erp.Site
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp-fresh.log 2>&1 &
echo "started pid $!, waiting for first-run provisioning..."
sleep 28

echo "=== [4] verify setup-status + company row ==="
token=$(curl -s -X POST http://127.0.0.1:5080/api/v3/en_US/auth/jwt/token -H 'Content-Type: application/json' -d '{"email":"erp@webvella.com","password":"erp"}' | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
echo "setup-status: $(curl -s http://127.0.0.1:5080/api/v3.0/p/agent/setup-status -H "Authorization: Bearer $token")"
echo "company rows: $(psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT name||' | '||legal_name||' | '||vat_number||' | '||currency FROM rec_company;")"
