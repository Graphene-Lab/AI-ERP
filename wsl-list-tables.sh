#!/usr/bin/env bash
export PGPASSWORD=dev
echo "=== tables matching product/warehouse/stock ==="
psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT tablename FROM pg_tables WHERE schemaname='public' AND (tablename ILIKE '%product%' OR tablename ILIKE '%wareh%' OR tablename ILIKE '%stock%') ORDER BY tablename;"
echo "=== total tables ==="
psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT count(*) FROM pg_tables WHERE schemaname='public';"
