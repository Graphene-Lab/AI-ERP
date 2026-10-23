#!/usr/bin/env bash
export PGPASSWORD=dev
echo "=== current stock SKU-2002 / WH1 ==="
psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT p.stock_quantity, si.quantity FROM rec_product p LEFT JOIN rec_stock_item si ON si.product_id=p.id AND si.warehouse_id=(SELECT id FROM rec_warehouse WHERE code='WH1') WHERE p.sku='SKU-2002';"
echo "=== active queries (locks/waiting) ==="
psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT pid, state, wait_event_type, wait_event, left(query,80) FROM pg_stat_activity WHERE datname='erp' AND state <> 'idle' ORDER BY pid;"
echo "=== advisory locks held ==="
psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT locktype, database, pid, mode FROM pg_locks WHERE locktype='advisory';"
echo "=== erp log tail ==="
tail -25 /tmp/erp.log
